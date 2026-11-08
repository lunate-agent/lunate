using Lunate.Ai;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

public sealed class ContainerEnvironmentTests
{
    private const string EnginePath = "/usr/bin/podman";

    private static readonly GuardCommand Unguarded = new(
        EnginePath,
        [],
        "none",
        Wrapped: false,
        Notice: null
    );

    [Fact]
    public async Task The_create_command_mounts_only_the_workspace_at_the_same_path()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine();
        using var environment = Build(engine);

        await environment.RunAsync(
            temp.Root,
            "echo hi",
            TimeSpan.FromSeconds(10),
            Unguarded,
            CancellationToken.None
        );

        ProcessRunRequest create = engine.Requests[0];
        Assert.Equal("create", create.Arguments[0]);
        Assert.Contains("--name", create.Arguments);
        Assert.Contains("lunate-tools-test", create.Arguments);
        string root = PathCanonical.Canonicalize(temp.Root);
        int volume = IndexOf(create.Arguments, "-v");
        Assert.Equal($"{root}:{root}", create.Arguments[volume + 1]);
        int workdir = IndexOf(create.Arguments, "-w");
        Assert.Equal(root, create.Arguments[workdir + 1]);
        Assert.Contains("alpine", create.Arguments);
        Assert.DoesNotContain(
            create.Arguments,
            argument =>
                argument.Contains(LunateHome.Store, StringComparison.Ordinal)
                || argument.Contains("auth.json", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task The_exec_command_forwards_the_session_marker_and_strips_the_pgid_line()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine();
        using var environment = Build(engine);

        ProcessRunResult result = await environment.RunAsync(
            temp.Root,
            "echo hi",
            TimeSpan.FromSeconds(10),
            Unguarded,
            CancellationToken.None
        );

        ProcessRunRequest exec = engine.Requests[^1];
        Assert.Equal("exec", exec.Arguments[0]);
        Assert.Contains("LUNATE_SESSION=1", exec.Arguments);
        Assert.Equal(
            PathCanonical.Canonicalize(temp.Root),
            exec.Arguments[IndexOf(exec.Arguments, "-w") + 1]
        );
        Assert.Contains(
            "echo LUNATE_PGRP:$(cut -d' ' -f5 /proc/$$/stat); exec sh -c \"$1\"",
            exec.Arguments
        );
        Assert.Equal("hello\n", result.Stdout);
        Assert.DoesNotContain("LUNATE_PGRP", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_commands_reuse_one_container()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine();
        using var environment = Build(engine);

        await environment.RunAsync(
            temp.Root,
            "one",
            TimeSpan.FromSeconds(10),
            Unguarded,
            CancellationToken.None
        );
        await environment.RunAsync(
            temp.Root,
            "two",
            TimeSpan.FromSeconds(10),
            Unguarded,
            CancellationToken.None
        );

        Assert.Single(engine.Requests, request => request.Arguments[0] == "create");
        Assert.Single(engine.Requests, request => request.Arguments[0] == "start");
        Assert.Equal(2, engine.Requests.Count(request => request.Arguments[0] == "exec"));
    }

    [Fact]
    public async Task Cancel_kills_the_container_process_group()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine { BlockExec = true };
        using var environment = Build(engine);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            environment.RunAsync(
                temp.Root,
                "sleep 30",
                TimeSpan.FromSeconds(30),
                Unguarded,
                cancellation.Token
            )
        );

        Assert.Contains(
            engine.Requests,
            request =>
                request.Arguments.Any(argument =>
                    argument.Contains("kill -TERM -4242", StringComparison.Ordinal)
                )
        );
        Assert.Contains(
            engine.Requests,
            request =>
                request.Arguments.Any(argument =>
                    argument.Contains("kill -KILL -4242", StringComparison.Ordinal)
                )
        );
    }

    [Fact]
    public async Task Dispose_removes_the_container()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine();
        var environment = Build(engine);
        await environment.RunAsync(
            temp.Root,
            "hi",
            TimeSpan.FromSeconds(10),
            Unguarded,
            CancellationToken.None
        );

        environment.Dispose();

        Assert.Contains(
            engine.Requests,
            request =>
                request.Arguments[0] == "rm" && request.Arguments.Contains("lunate-tools-test")
        );
    }

    [Fact]
    public async Task A_missing_engine_is_a_teaching_error_and_never_a_host_run()
    {
        using var temp = new TempDirectory();
        var engine = new FakeEngine();
        using var environment = new ContainerEnvironment(
            new ExecutionEnvironmentSelection("docker", "alpine"),
            findOnPath: _ => null,
            run: engine.Run,
            newName: () => "lunate-tools-test"
        );

        Assert.NotNull(environment.Unavailable);
        ContainerEnvironmentException exception =
            await Assert.ThrowsAsync<ContainerEnvironmentException>(() =>
                environment.RunAsync(
                    temp.Root,
                    "echo hi",
                    TimeSpan.FromSeconds(10),
                    Unguarded,
                    CancellationToken.None
                )
            );
        Assert.Contains("docker", exception.Message, StringComparison.Ordinal);
        Assert.Empty(engine.Requests);
    }

    [Fact]
    public async Task A_workspace_path_with_a_colon_is_refused()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "A colon cannot name a Windows directory.");
        using var temp = new TempDirectory();
        string root = Path.Combine(temp.Root, "with:colon");
        Directory.CreateDirectory(root);
        var engine = new FakeEngine();
        using var environment = Build(engine);

        ContainerEnvironmentException exception =
            await Assert.ThrowsAsync<ContainerEnvironmentException>(() =>
                environment.RunAsync(
                    root,
                    "echo hi",
                    TimeSpan.FromSeconds(10),
                    Unguarded,
                    CancellationToken.None
                )
            );

        Assert.Contains("':'", exception.Message, StringComparison.Ordinal);
        Assert.Empty(engine.Requests);
    }

    private static ContainerEnvironment Build(FakeEngine engine) =>
        new(
            new ExecutionEnvironmentSelection("podman", "alpine"),
            findOnPath: _ => EnginePath,
            run: engine.Run,
            newName: () => "lunate-tools-test"
        );

    private static int IndexOf(IReadOnlyList<string> arguments, string value)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == value)
            {
                return index;
            }
        }

        throw new InvalidOperationException($"'{value}' is absent from the arguments");
    }

    private sealed class FakeEngine
    {
        internal List<ProcessRunRequest> Requests { get; } = [];

        internal bool BlockExec { get; init; }

        internal async Task<ProcessRunResult> Run(
            ProcessRunRequest request,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task>? onCancel,
            Action<string>? onFirstStdoutLine
        )
        {
            Requests.Add(request);
            string first = request.Arguments[0];
            if (first is "create" or "start" or "rm")
            {
                return Exit(0);
            }

            if (
                request.Arguments.Any(argument =>
                    argument.Contains("kill -0", StringComparison.Ordinal)
                )
            )
            {
                return Exit(1);
            }

            if (
                request.Arguments.Any(argument =>
                    argument.Contains("kill -", StringComparison.Ordinal)
                )
            )
            {
                return Exit(0);
            }

            if (BlockExec)
            {
                onFirstStdoutLine?.Invoke("LUNATE_PGRP:4242");
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (onCancel is not null)
                    {
                        await onCancel(CancellationToken.None);
                    }

                    throw;
                }
            }

            onFirstStdoutLine?.Invoke("LUNATE_PGRP:4242");
            return new ProcessRunResult(0, "LUNATE_PGRP:4242\nhello\n", string.Empty, false);
        }

        private static ProcessRunResult Exit(int code) =>
            new(code, string.Empty, string.Empty, false);
    }
}
