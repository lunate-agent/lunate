using System.Text.Json;
using Lunate.Agent;
using Lunate.Contracts;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The execution-environment seam: the local description and identity map, Bash and ripgrep going
/// through an injected environment, the change scan staying local, and the extension process path
/// delegating both of its capabilities to the same environment.
/// </summary>
public sealed class ExecutionEnvironmentTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public void Local_execution_describes_itself_as_local_and_maps_nothing()
    {
        IExecutionEnvironment environment = LocalExecutionEnvironment.Instance;
        var command = new ExecutionCommand(
            "/bin/sh",
            ["-c", "true"],
            Path.GetTempPath(),
            new Dictionary<string, string> { ["A"] = "B" }
        );

        Assert.Equal("local", environment.Description);
        Assert.Same(command, environment.Map(command));
    }

    [Fact]
    public async Task Bash_runs_through_the_environment_and_the_change_scan_stays_local()
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
        var environment = new RecordingEnvironment(LocalExecutionEnvironment.Instance);
        var tool = new BashTool(
            new Workspace(repo.Root),
            resolver: null,
            timeout: null,
            log: null,
            new BashGuard(),
            changeScanTimeout: null,
            guardEnabled: false,
            environment
        );

        // The first call baselines the change tracker; the edit then exercises the host scan.
        await tool.ExecuteAsync(CommandArgs("echo warm"), context, Ct);
        sink.Clear();
        ToolResult result = await tool.ExecuteAsync(
            CommandArgs("echo class A changed > a.cs"),
            context,
            Ct
        );

        Assert.False(result.IsError, result.Output);
        FileChangeEvent change = Assert.Single(sink.Events);
        Assert.Equal("a.cs", change.RelativePath);
        Assert.Equal(2, environment.Commands.Count);
        Assert.DoesNotContain(
            environment.Commands,
            command => string.Equals(command.Executable, "git", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Ripgrep_runs_through_the_environment()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("a.txt"), "needle\n");
        var environment = new RecordingEnvironment(LocalExecutionEnvironment.Instance)
        {
            Result = _ => new ProcessRunResult(
                0,
                """
                {"type":"match","data":{"path":{"text":"a.txt"},"line_number":1,"lines":{"text":"needle\n"}}}
                """,
                string.Empty,
                TimedOut: false
            ),
        };
        var engine = new RipgrepGrepEngine("rg", environment: environment);
        var request = new GrepRequest(
            Pattern: "needle",
            Root: temp.Root,
            RelativeRoot: ".",
            SearchIsFile: false,
            Glob: null,
            CaseSensitive: true,
            MaxResults: 100
        );

        GrepSearch search = await engine.SearchAsync(request, Ct);

        Assert.Null(search.Error);
        GrepMatch match = Assert.Single(search.Matches);
        Assert.Equal("a.txt", match.Path);
        ExecutionCommand command = Assert.Single(environment.Commands);
        Assert.Equal("rg", command.Executable);
        Assert.Contains("--json", command.Arguments);
    }

    [Fact]
    public async Task Process_runner_delegates_both_capabilities_to_the_environment()
    {
        using var temp = new TempDirectory();
        var environment = new RecordingEnvironment(LocalExecutionEnvironment.Instance)
        {
            Result = _ => new ProcessRunResult(0, "delegated", string.Empty, TimedOut: false),
        };
        var runner = new ProcessRunner(environment);

        ProcessRunResult result = await runner.RunAsync(
            new ProcessRunRequest("/bin/true", [], temp.Root),
            Ct
        );
        InvalidOperationException sessionFailure =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.StartSessionAsync(
                    new ProcessSessionRequest(
                        "/bin/true",
                        [],
                        temp.Root,
                        new Dictionary<string, string>()
                    ),
                    Ct
                )
            );

        Assert.Equal("delegated", result.Stdout);
        Assert.Equal("delegated", sessionFailure.Message);
        Assert.Single(environment.Commands);
        Assert.Single(environment.Sessions);
    }

    private static JsonElement CommandArgs(string command) =>
        JsonSerializer.SerializeToElement(new { command });

    private sealed class RecordingEnvironment(IExecutionEnvironment inner) : IExecutionEnvironment
    {
        public List<ExecutionCommand> Commands { get; } = [];

        public List<ProcessSessionRequest> Sessions { get; } = [];

        public Func<ProcessRunRequest, ProcessRunResult>? Result { get; init; }

        public string Description => inner.Description;

        public ExecutionCommand Map(ExecutionCommand command)
        {
            Commands.Add(command);
            return inner.Map(command);
        }

        public Task<ProcessRunResult> RunAsync(
            ProcessRunRequest request,
            CancellationToken cancellationToken
        )
        {
            ExecutionCommand mapped = Map(
                new ExecutionCommand(
                    request.FileName,
                    request.Arguments,
                    request.WorkingDirectory,
                    request.Environment,
                    request.ExtraEnvironment
                )
            );
            if (Result is { } result)
            {
                return Task.FromResult(result(request));
            }

            return inner.RunAsync(
                request with
                {
                    FileName = mapped.Executable,
                    Arguments = mapped.Arguments,
                    WorkingDirectory = mapped.WorkingDirectory,
                    Environment = mapped.Environment,
                    ExtraEnvironment = mapped.ExtraEnvironment,
                },
                cancellationToken
            );
        }

        public Task<IProcessSession> StartSessionAsync(
            ProcessSessionRequest request,
            CancellationToken cancellationToken
        )
        {
            Sessions.Add(request);
            return Task.FromException<IProcessSession>(new InvalidOperationException("delegated"));
        }

        public ValueTask CancelAsync(IExecutionProcess process) => inner.CancelAsync(process);
    }

    private sealed class RecordingChangeSink : IFileChangeSink
    {
        private readonly List<FileChangeEvent> _events = [];

        public IReadOnlyList<FileChangeEvent> Events => _events;

        public void Notify(FileChangeEvent change) => _events.Add(change);

        public void Clear() => _events.Clear();
    }

    private sealed class NullAgentEvents : IAgentEvents
    {
        public void Emit(AgentEvent agentEvent) { }
    }
}
