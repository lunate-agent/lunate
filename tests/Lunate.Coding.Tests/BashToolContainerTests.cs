using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

public sealed class BashToolContainerTests
{
    private static readonly ToolContext Context = new("run", new NullAgentEvents());

    [Fact]
    public async Task A_container_call_returns_the_exec_output_and_keeps_the_result_contract()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine();
        using var environment = new ContainerEnvironment(
            new ExecutionEnvironmentSelection("podman", "alpine"),
            findOnPath: _ => "/usr/bin/podman",
            run: engine.Run,
            newName: () => "lunate-tools-test"
        );
        var tool = new BashTool(
            new Workspace(temp.Root),
            null,
            TimeSpan.FromSeconds(10),
            null,
            new BashGuard(),
            changeScanTimeout: null,
            guardEnabled: true,
            environment: environment
        );

        ToolResult result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"command":"echo hi"}""").RootElement,
            Context,
            CancellationToken.None
        );

        Assert.False(result.IsError);
        Assert.Equal("hello\nexit code: 0", result.Output);
        Assert.Contains(
            engine.Requests,
            request => request.Arguments.Contains("LUNATE_SESSION=1", StringComparer.Ordinal)
        );
    }

    [Fact]
    public async Task A_missing_engine_is_an_error_that_names_the_engine()
    {
        using var temp = new TempDirectory();
        using var environment = new ContainerEnvironment(
            new ExecutionEnvironmentSelection("docker", "alpine"),
            findOnPath: _ => null,
            run: (_, _, _, _) => throw new InvalidOperationException("must not run"),
            newName: () => "lunate-tools-test"
        );
        var tool = new BashTool(
            new Workspace(temp.Root),
            null,
            TimeSpan.FromSeconds(10),
            null,
            new BashGuard(),
            changeScanTimeout: null,
            guardEnabled: true,
            environment: environment
        );

        ToolResult result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"command":"echo hi"}""").RootElement,
            Context,
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Contains("docker", result.Output, StringComparison.Ordinal);
        Assert.Contains("PATH", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_container_write_is_published_by_the_host_change_scan()
    {
        using var repo = new GitTestRepo();
        repo.Write("a.cs", "class A { }\n");
        repo.CommitAll();
        var sink = new RecordingChangeSink();
        var context = new ToolContext(repo.Root, new NullAgentEvents())
        {
            FileChanges = sink,
            RunId = "run_1",
        };
        var engine = new FakeEngine();
        using var environment = new ContainerEnvironment(
            new ExecutionEnvironmentSelection("podman", "alpine"),
            findOnPath: _ => "/usr/bin/podman",
            run: engine.Run,
            newName: () => "lunate-tools-test"
        );
        var tool = new BashTool(
            new Workspace(repo.Root),
            null,
            TimeSpan.FromSeconds(10),
            null,
            new BashGuard(),
            changeScanTimeout: null,
            guardEnabled: true,
            environment: environment
        );

        await tool.ExecuteAsync(
            JsonDocument.Parse("""{"command":"echo warm"}""").RootElement,
            context,
            CancellationToken.None
        );
        sink.Clear();
        engine.OnExec = () => repo.Write("a.cs", "class A { int x; }\n");
        ToolResult result = await tool.ExecuteAsync(
            JsonDocument.Parse("""{"command":"write a.cs"}""").RootElement,
            context,
            CancellationToken.None
        );

        Assert.False(result.IsError, result.Output);
        FileChangeEvent change = Assert.Single(sink.Events);
        Assert.Equal("a.cs", change.RelativePath);
        Assert.Equal("bash", change.Source);
    }

    private sealed class RecordingChangeSink : IFileChangeSink
    {
        private readonly List<FileChangeEvent> _events = [];

        internal IReadOnlyList<FileChangeEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }

        public void Notify(FileChangeEvent change)
        {
            lock (_events)
            {
                _events.Add(change);
            }
        }

        internal void Clear()
        {
            lock (_events)
            {
                _events.Clear();
            }
        }
    }

    private sealed class FakeEngine
    {
        internal List<ProcessRunRequest> Requests { get; } = [];

        internal Action? OnExec { get; set; }

        internal Task<ProcessRunResult> Run(
            ProcessRunRequest request,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task>? onCancel,
            Action<string>? onFirstStdoutLine
        )
        {
            Requests.Add(request);
            if (request.Arguments[0] is "create" or "start" or "rm")
            {
                return Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty, false));
            }

            OnExec?.Invoke();
            onFirstStdoutLine?.Invoke("LUNATE_PGRP:7");
            return Task.FromResult(
                new ProcessRunResult(0, "LUNATE_PGRP:7\nhello\n", string.Empty, false)
            );
        }
    }
}
