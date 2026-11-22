using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility;
using Lunate.Extensibility.Abstractions;
using Lunate.Extensibility.Testing;

namespace NodeRemoteExtension.Tests;

/// <summary>
/// The node reference extension's fitness tests: the conformance runner drives the real script
/// over the real wire, and the production composition test loads it through the real host — a
/// hook is delivered (and its block reaches the pipeline) and its registered service answers a
/// host call, with no test doubles anywhere.
/// </summary>
public sealed class NodeRemoteExtensionTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_reference_extension_conforms_on_the_wire()
    {
        RemoteConformanceReport report = await RemoteConformanceRunner.RunAsync(
            new RemoteConformanceOptions
            {
                FileName = "node",
                Arguments = ["index.js"],
                WorkingDirectory = SampleExtension.Directory,
                CancelCommand = "wait",
                FailingCommand = "fail",
            },
            Ct
        );

        Assert.True(report.Succeeded, report.Summary);
        Assert.Contains(report.Steps, step =>
            step.Name == "service call child -> host" && step.Passed
        );
        Assert.Contains(report.Steps, step =>
            step.Name == "service call host -> child" && step.Passed
        );
        Assert.Contains(report.Steps, step => step.Name == "command invoke" && step.Passed);
        Assert.Contains(report.Steps, step => step.Name == "command cancel" && step.Passed);
        Assert.Contains(report.Steps, step => step.Name == "command failure" && step.Passed);
        Assert.Contains(report.Steps, step => step.Name == "event notification" && step.Passed);
        Assert.Contains(report.Steps, step => step.Name == "shutdown" && step.Passed);
    }

    [Fact]
    public async Task The_reference_extension_observes_a_real_turn_over_the_wire()
    {
        using var temp = new TempDirectory();
        await using var host = new ExtensionTestHost(
            new ExtensionTestHostOptions
            {
                TempDirectory = temp.Root,
                ExtensionId = "node-remote",
                ExtensionDirectory = SampleExtension.Directory,
            }
        );

        // The manifest declares `events`, so the extension starts at session start and subscribed
        // before the turn: the child observes it from `run_started` to `run_finished`.
        ExtensionRunResult turn = await host.RunAsync(
            new SingleReplyChatClient("hello from the node reference"),
            "say hello",
            Ct
        );
        Assert.Contains(turn.Events, agentEvent => agentEvent is RunStarted);
        Assert.Contains(turn.Events, agentEvent => agentEvent is RunFinished);

        // The projection is never awaited (ADR-0040): the child's pump may still be draining the
        // turn when it finishes, so the receipt is polled for a bounded window like the
        // conformance runner does.
        RegisteredCommandSource commands = Assert.Single(host.Runner.CommandSources.List());
        string said = string.Empty;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            CommandResult observed = await commands.Source.InvokeAsync(
                new CommandInvocation("observed", string.Empty),
                Ct
            );
            Assert.False(observed.IsError);
            said = observed.Message ?? string.Empty;
            if (said.Contains("run_finished", StringComparison.Ordinal))
            {
                break;
            }

            await Task.Delay(30, Ct);
        }

        int started = said.IndexOf("run_started", StringComparison.Ordinal);
        int finished = said.IndexOf("run_finished", StringComparison.Ordinal);
        Assert.True(started >= 0, $"the child did not observe run_started: {said}");
        Assert.True(finished > started, $"run_finished did not follow run_started: {said}");
        // The turn's text events arrived too: the whole stream, not the lifecycle markers alone.
        Assert.Contains("text_message_content", said, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_reference_extension_runs_through_the_real_host()
    {
        using var temp = new TempDirectory();
        await using var host = new ExtensionTestHost(
            new ExtensionTestHostOptions
            {
                TempDirectory = temp.Root,
                ExtensionId = "node-remote",
                ExtensionDirectory = SampleExtension.Directory,
            }
        );
        await host.StartAsync(Ct);
        // The child process spawns and initializes here, not inside the first hook's budget: on a
        // loaded Windows runner that cold start spent the whole handler timeout.
        Assert.True(await host.WarmUpAsync(cancellationToken: Ct));

        // The hook is delivered: the script consumes the host's core/workspace service and blocks
        // the probe call with a reason naming the workspace it read through that service.
        ToolCallingResult blocked = await host.Runner.Hooks.RunToolCallingAsync(
            new ToolCallingPayload(
                "run-1",
                "call-1",
                "read",
                JsonDocument.Parse("""{"path":"block-me.txt"}""").RootElement.Clone()
            ),
            Ct
        );

        var block = Assert.IsType<ToolCallingResult.Block>(blocked.Value);
        Assert.Contains(
            host.WorkingDirectory,
            block.Reason,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal
        );

        // A different call proceeds: the hook observes every call, it does not block blindly.
        ToolCallingResult proceeded = await host.Runner.Hooks.RunToolCallingAsync(
            new ToolCallingPayload(
                "run-1",
                "call-2",
                "read",
                JsonDocument.Parse("""{"path":"ok.txt"}""").RootElement.Clone()
            ),
            Ct
        );
        Assert.IsType<ToolCallingResult.Proceed>(proceeded.Value);

        // The extension's registered service answers a host call through the registry.
        Assert.True(host.Runner.Services.TryGet("echo", out IBackgroundService? registered));
        var service = Assert.IsAssignableFrom<IJsonService>(registered);
        JsonElement? answer = await service.InvokeAsync(
            "repeat",
            JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone(),
            Ct
        );

        Assert.Equal("hello", answer!.Value.GetProperty("repeated").GetString());

        // The manifest command dispatches over the wire through the loader's own source: the child
        // receives the bare name and the remainder, and its reply becomes the notice naming the
        // source-qualified id.
        RegisteredCommandSource commands = Assert.Single(host.Runner.CommandSources.List());
        Assert.Equal(
            ["ping", "wait", "fail", "observed"],
            [.. commands.Descriptors.Select(descriptor => descriptor.Name)]
        );
        CommandResult ping = await commands.Source.InvokeAsync(
            new CommandInvocation("ping", "hello from the host"),
            Ct
        );
        Assert.False(ping.IsError);
        Assert.Equal("ext:node-remote:ping: pong hello from the host", ping.Message);

        // A failing command raises the failure for the registry to shape; the session continues.
        Exception failure = await Assert.ThrowsAnyAsync<Exception>(() =>
            commands.Source.InvokeAsync(new CommandInvocation("fail", string.Empty), Ct).AsTask()
        );
        Assert.Equal("the node reference extension fails this command on purpose", failure.Message);

        // Session end: the shutdown request reaches the script, which exits; nothing is killed.
        await host.StopAsync(Ct);
        Assert.False(host.Log.Contains("being killed"));
    }
}
