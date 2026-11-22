using Microsoft.Extensions.AI;
using static Lunate.Agent.Tests.AgentTestSupport;
using static Lunate.Agent.Tests.ParallelToolTestSupport;

namespace Lunate.Agent.Tests;

public sealed class AgentParallelToolSchedulingTests
{
    [Fact]
    public async Task The_concurrency_cap_is_respected()
    {
        string[] paths = [.. Enumerable.Range(1, 7).Select(index => $"{index}.txt")];
        var probe = new ConcurrencyProbe();
        ScriptedTool read = ReadTool("contents");
        read.OnExecuteAsync = async (_, _, ct) =>
        {
            probe.Enter();
            try
            {
                await Task.Delay(80, ct);
                return new ToolResult("contents", IsError: false);
            }
            finally
            {
                probe.Exit();
            }
        };
        var harness = new AgentHarness(ClientFor(paths), Registry(read));

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(paths.Length, events.OfType<ToolCallResult>().Count());
        Assert.InRange(probe.Maximum, 2, AgentHarness.MaxParallelReadOnlyCalls);
    }

    [Fact]
    public async Task A_mixed_batch_stays_serial_in_call_order()
    {
        var probe = new ConcurrencyProbe();
        ScriptedTool read = ReadTool("contents");
        ScriptedTool write = new(
            "write",
            "Writes a file.",
            """{"type":"object","properties":{"path":{"type":"string"}}}""",
            ToolRisk.Write
        );
        read.OnExecuteAsync = (_, _, ct) => Tracked(probe, "read", ct);
        write.OnExecuteAsync = (_, _, ct) => Tracked(probe, "write", ct);
        var client = new ScriptedChatClient()
            .Enqueue(
                LoopScripts.Calls(
                    new FunctionCallContent("call_1", "read", LoopScripts.Args(("path", "a.txt"))),
                    new FunctionCallContent("call_2", "write", LoopScripts.Args(("path", "b.txt")))
                ),
                LoopScripts.ToolCalls()
            )
            .Enqueue(LoopScripts.Text("Done"), LoopScripts.Stop());
        var harness = new AgentHarness(client, Registry(read, write));

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(1, probe.Maximum);
        Assert.Equal(
            ["call_1", "call_2"],
            events.OfType<ToolCallResult>().Select(result => result.CallId)
        );
    }

    [Fact]
    public async Task A_sequential_read_keeps_the_whole_batch_serial()
    {
        var probe = new ConcurrencyProbe();
        ScriptedTool read = DelayedRead(40);
        read.Concurrency = ToolConcurrency.Sequential;
        read.OnExecuteAsync = (_, _, ct) => Tracked(probe, "contents", ct);
        var harness = new AgentHarness(ClientFor(["a.txt", "b.txt"]), Registry(read));

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(1, probe.Maximum);
        Assert.Equal(2, events.OfType<ToolCallResult>().Count());
    }

    [Fact]
    public async Task Cancelling_a_batch_leaves_a_valid_stream_and_history()
    {
        ScriptedTool read = ReadTool("contents");
        int started = 0;
        var bothStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        read.OnExecuteAsync = async (_, _, ct) =>
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                bothStarted.TrySetResult();
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new ToolResult("never", IsError: false);
        };
        var client = ClientFor(["a.txt", "b.txt"]);
        var harness = new AgentHarness(client, Registry(read));
        using var cancellation = new CancellationTokenSource();

        Task<List<AgentEvent>> running = harness
            .RunAsync("go", cancellation.Token)
            .ToListAsync(TestContext.Current.CancellationToken);
        await bothStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
        cancellation.Cancel();
        List<AgentEvent> events = await running;

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(2, events.OfType<ToolCallResult>().Count(result => result.IsError));
        Assert.All(events.OfType<ToolCallResult>(), result =>
            Assert.Contains("cancelled by the user", result.Output)
        );
        Assert.Equal(
            StopReasons.Cancelled,
            Assert.IsType<RunFinished>(events[^1].Value).StopReason
        );

        client.Enqueue(LoopScripts.Text("Recovered"), LoopScripts.Stop());
        List<AgentEvent> next = await Run(harness);
        Assert.Empty(EventSequenceValidator.Validate(next));
        Assert.Equal(
            2,
            client
                .Requests[^1]
                .Messages.Count(message =>
                    message.Role == ChatRole.Tool
                    && message
                        .Contents.OfType<FunctionResultContent>()
                        .Any(result => result.Result!.ToString()!.Contains("cancelled by the user"))
                )
        );
    }

    private static async Task<ToolResult> Tracked(
        ConcurrencyProbe probe,
        string output,
        CancellationToken ct
    )
    {
        probe.Enter();
        try
        {
            await Task.Delay(40, ct);
            return new ToolResult(output, IsError: false);
        }
        finally
        {
            probe.Exit();
        }
    }
}
