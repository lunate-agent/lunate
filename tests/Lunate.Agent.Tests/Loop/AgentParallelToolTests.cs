using System.Diagnostics;
using Microsoft.Extensions.AI;
using static Lunate.Agent.Tests.AgentTestSupport;
using static Lunate.Agent.Tests.ParallelToolTestSupport;

namespace Lunate.Agent.Tests;

public sealed class AgentParallelToolTests
{
    [Fact]
    public async Task Five_independent_reads_finish_faster_than_the_serial_path()
    {
        const int callCount = 5;
        const int delayMs = 120;
        string[] paths = [.. Enumerable.Range(1, callCount).Select(index => $"{index}.txt")];

        ScriptedTool serialRead = DelayedRead(delayMs);
        serialRead.Concurrency = ToolConcurrency.Sequential;
        var serialHarness = new AgentHarness(ClientFor(paths), Registry(serialRead));

        ScriptedTool parallelRead = DelayedRead(delayMs);
        var parallelHarness = new AgentHarness(ClientFor(paths), Registry(parallelRead));

        var serialWatch = Stopwatch.StartNew();
        List<AgentEvent> serialEvents = await Run(serialHarness);
        serialWatch.Stop();
        var parallelWatch = Stopwatch.StartNew();
        List<AgentEvent> parallelEvents = await Run(parallelHarness);
        parallelWatch.Stop();

        Assert.Equal(callCount, serialEvents.OfType<ToolCallResult>().Count());
        Assert.Equal(callCount, parallelEvents.OfType<ToolCallResult>().Count());
        Assert.True(
            parallelWatch.Elapsed < serialWatch.Elapsed * 0.75,
            $"parallel batch took {parallelWatch.Elapsed} against {serialWatch.Elapsed} serial"
        );
    }

    [Fact]
    public async Task Event_order_and_each_result_id_are_deterministic()
    {
        string[] paths = ["a.txt", "b.txt", "c.txt", "d.txt"];
        var client = ClientFor(paths);

        List<AgentEvent> first = await Run(
            new AgentHarness(client, Registry(ReadTool("contents")))
        );
        List<AgentEvent> second = await Run(
            new AgentHarness(ClientFor(paths), Registry(ReadTool("contents")))
        );

        string[] callIds = ["call_1", "call_2", "call_3", "call_4"];
        Assert.Empty(EventSequenceValidator.Validate(first));
        Assert.Empty(EventSequenceValidator.Validate(second));
        Assert.Equal(callIds, first.OfType<ToolCallStart>().Select(start => start.CallId));
        Assert.Equal(callIds, first.OfType<ToolCallResult>().Select(result => result.CallId));
        Assert.Equal(
            callIds,
            client
                .Requests[1]
                .Messages.Where(message => message.Role == ChatRole.Tool)
                .SelectMany(message => message.Contents.OfType<FunctionResultContent>())
                .Select(result => result.CallId)
        );
        Assert.Equal(
            EventSequenceSnapshot.Serialize(first),
            EventSequenceSnapshot.Serialize(second)
        );
    }

    [Fact]
    public async Task A_declined_call_does_not_cancel_its_siblings()
    {
        string[] paths = ["a.txt", "b.txt", "c.txt"];
        ScriptedTool read = ReadTool("contents");
        int executions = 0;
        read.OnExecute = (_, _) =>
        {
            Interlocked.Increment(ref executions);
            return new ToolResult("contents", IsError: false);
        };
        var approver = new SelectiveApprover(path =>
            !string.Equals(path, "b.txt", StringComparison.Ordinal)
        );
        var harness = new AgentHarness(
            ClientFor(paths),
            Registry(read),
            new AgentHarnessOptions { Approver = approver }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(3, approver.Requests);
        Assert.Equal(2, executions);
        Dictionary<string, ToolCallResult> results = events
            .OfType<ToolCallResult>()
            .ToDictionary(result => result.CallId, StringComparer.Ordinal);
        Assert.False(results["call_1"].IsError);
        Assert.True(results["call_2"].IsError);
        Assert.Contains("Denied", results["call_2"].Output, StringComparison.Ordinal);
        Assert.False(results["call_3"].IsError);
    }

    [Fact]
    public async Task A_failing_call_leaves_its_siblings_running()
    {
        string[] paths = ["a.txt", "b.txt", "c.txt"];
        ScriptedTool read = ReadTool("contents");
        read.OnExecuteAsync = (args, _, _) =>
        {
            string path = args.GetProperty("path").GetString()!;
            return path == "b.txt"
                ? Task.FromException<ToolResult>(new InvalidOperationException("disk exploded"))
                : Task.FromResult(new ToolResult("contents", IsError: false));
        };
        var harness = new AgentHarness(ClientFor(paths), Registry(read));

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Dictionary<string, ToolCallResult> results = events
            .OfType<ToolCallResult>()
            .ToDictionary(result => result.CallId, StringComparer.Ordinal);
        Assert.False(results["call_1"].IsError);
        Assert.True(results["call_2"].IsError);
        Assert.Contains("disk exploded", results["call_2"].Output, StringComparison.Ordinal);
        Assert.False(results["call_3"].IsError);
    }

    [Fact]
    public async Task The_web_read_tools_join_the_batch_despite_execute_risk()
    {
        string[] queries = ["one", "two"];
        var probe = new ConcurrencyProbe();
        ScriptedTool web = new(
            "web_search",
            "Searches the web.",
            """{"type":"object","properties":{"query":{"type":"string"}}}""",
            ToolRisk.Execute
        );
        web.OnExecuteAsync = async (_, _, ct) =>
        {
            probe.Enter();
            try
            {
                await Task.Delay(60, ct);
                return new ToolResult("result", IsError: false);
            }
            finally
            {
                probe.Exit();
            }
        };
        var harness = new AgentHarness(ClientFor(queries, toolName: "web_search"), Registry(web));

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(2, events.OfType<ToolCallResult>().Count());
        Assert.InRange(probe.Maximum, 2, AgentHarness.MaxParallelReadOnlyCalls);
    }

    private sealed class SelectiveApprover(Func<string, bool> decide) : IToolApprover
    {
        public int Requests { get; private set; }

        public ValueTask<bool> ApproveAsync(ApprovalRequest request, CancellationToken ct)
        {
            Requests++;
            string path = request.Args.GetProperty("path").GetString()!;
            return ValueTask.FromResult(decide(path));
        }
    }
}
