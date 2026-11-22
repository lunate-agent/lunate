using System.Globalization;
using Acp.Schema;
using Lunate.Agent;
using Lunate.Coding;
using Lunate.Extensibility;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Protocols.Tests;

/// <summary>
/// ADR-0040 on the ACP path: a serve composes an extension host, so an in-process observer sees an
/// editor session's turn too. The protocol loop publishes every event it receives before ACP's own
/// filtering, so child-run events and lifecycle markers reach the stream even though
/// <c>session/update</c> never carries them.
/// </summary>
public sealed class AcpEventStreamTests
{
    private const string ExtensionId = "acp-observer";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_observer_sees_an_acp_prompt_from_start_to_end_child_runs_included()
    {
        using var temp = new TempDirectory();
        string marker = temp.File("observed.txt");
        await using ExtensionSession host = await ExtensionSession.StartAsync(
            new WorkspaceInfo(temp.File("worktree"), null, null),
            new ExtensionSeams { StorePath = temp.File("store"), BuiltIns = [Observer(marker)] },
            Ct
        );

        var model = new AcpScriptedChatClient()
            .Enqueue(Call("spawn", ("prompt", "child")), AcpScripts.ToolCalls())
            .Enqueue(Call("echo", ("message", "hi")), AcpScripts.ToolCalls())
            .Enqueue(AcpScripts.Text("child answer"), AcpScripts.Stop())
            .Enqueue(AcpScripts.Text("parent answer"), AcpScripts.Stop());
        await using AcpRuntime runtime = AcpTestSupport.Start(
            _ => AcpTestSupport.Harness(model, new SpawnTool(), new EchoTool()),
            createServer: log => AcpMode.CreateServer(log, host)
        );

        await runtime.Client.InitializeAsync(AcpTestSupport.InitializeRequest, Ct);
        NewSessionResponse session = await runtime.Client.NewSessionAsync(
            new NewSessionRequest { Cwd = temp.Root, McpServers = [] },
            Ct
        );

        PromptResponse response = await runtime
            .Client.PromptAsync(
                new PromptRequest
                {
                    SessionId = session.SessionId,
                    Prompt = [new TextContent { Text = "go" }],
                },
                Ct
            )
            .WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Assert.Equal(StopReason.EndTurn, response.StopReason);
        // Nothing was intercepted: the observer registered no hook handler.
        Assert.Equal(0, host.HookRunner.Count(ExtensionId));
        await WaitForDeliveryAsync(host);

        string[] observed = File.ReadAllLines(marker);
        Assert.Equal("RunStarted|-", observed[0]);
        Assert.Contains(observed, line =>
            line.StartsWith("TextMessageEnd|", StringComparison.Ordinal)
        );
        // A child run's start carries its parent's run id in the second column.
        int childStart = Array.FindIndex(observed, line =>
            line.StartsWith("RunStarted|", StringComparison.Ordinal)
            && !line.EndsWith("|-", StringComparison.Ordinal)
        );
        Assert.True(
            childStart > 0,
            "the child run's events never reached the stream; observed:\n"
                + string.Join('\n', observed)
        );
        int parentFinish = Array.FindLastIndex(observed, line => line == "RunFinished|-");
        Assert.True(
            parentFinish > childStart,
            "the child run's events did not arrive before the parent finished; observed:\n"
                + string.Join('\n', observed)
        );
    }

    /// <summary>
    /// Waits until the subscription's pump has delivered everything the run published: ACP publishes
    /// on the protocol loop before it answers the prompt, so after the response the counters settle.
    /// </summary>
    private static async Task WaitForDeliveryAsync(ExtensionSession host)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            ExtensionEventStreamStatus status = host.EventStatus(ExtensionId);
            if (status.Received > 0 && status.Delivered == status.Received)
            {
                return;
            }

            await Task.Delay(25, Ct);
        }

        ExtensionEventStreamStatus stuck = host.EventStatus(ExtensionId);
        throw new TimeoutException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"the extension's event pump did not drain: received {stuck.Received}, delivered {stuck.Delivered}"
            )
        );
    }

    private static Microsoft.Extensions.AI.ChatResponseUpdate Call(
        string tool,
        (string Key, object? Value) argument
    ) =>
        AcpScripts.Call(
            "call-1",
            tool,
            new Dictionary<string, object?> { [argument.Key] = argument.Value }
        );

    /// <summary>The observer: one subscription on the stream, nothing registered, no hooks.</summary>
    private static BuiltInExtension Observer(string marker) =>
        new(
            "host/acp-observer/extension.json",
            ExtensionManifest.Parse(
                $$"""{"id":"{{ExtensionId}}","version":"0.1.0","apiVersion":"^1.0.0","builtin":true,"events":["*"]}""",
                "host/acp-observer/extension.json"
            ),
            () => new ObserverFactory(marker)
        );

    private sealed class ObserverFactory(string marker) : IExtensionFactory
    {
        public IExtension Create(IExtensionContext context)
        {
            context.Events.Subscribe(new MarkerObserver(marker));
            return new ObserverShell();
        }
    }

    private sealed class ObserverShell : IExtension;

    /// <summary>
    /// Appends one <c>Kind|ParentRunId</c> line per event, so the test reads the turn's shape — and
    /// whether a line belongs to a child run — from disk after the prompt answered.
    /// </summary>
    private sealed class MarkerObserver(string marker) : IObserver<AgentEvent>
    {
        private readonly object _gate = new();

        public void OnNext(AgentEvent value) =>
            Append(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{value.Kind.Name}|{value.ParentRunId ?? "-"}"
                )
            );

        public void OnError(Exception error) => Append("error|" + error.GetType().Name);

        public void OnCompleted() => Append("completed|-");

        private void Append(string line)
        {
            lock (_gate)
            {
                File.AppendAllText(marker, line + "\n");
            }
        }
    }
}
