using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>Scripts and probes shared by the parallel read-only batch tests.</summary>
internal static class ParallelToolTestSupport
{
    internal static ScriptedChatClient ClientFor(
        IReadOnlyList<string> paths,
        string toolName = "read"
    ) =>
        new ScriptedChatClient()
            .Enqueue(
                LoopScripts.Calls(
                    [
                        .. paths.Select(
                            (path, index) =>
                                new FunctionCallContent(
                                    $"call_{index + 1}",
                                    toolName,
                                    LoopScripts.Args(("path", path))
                                )
                        ),
                    ]
                ),
                LoopScripts.ToolCalls()
            )
            .Enqueue(LoopScripts.Text("Done"), LoopScripts.Stop());

    internal static ScriptedTool DelayedRead(int delayMs, string toolName = "read")
    {
        ScriptedTool read = AgentTestSupport.ReadTool("contents", toolName);
        read.OnExecuteAsync = async (_, _, ct) =>
        {
            await Task.Delay(delayMs, ct);
            return new ToolResult("contents", IsError: false);
        };
        return read;
    }
}

/// <summary>Records the highest number of tool bodies that were inside their execution at once.</summary>
internal sealed class ConcurrencyProbe
{
    private int _active;
    private int _maximum;

    internal int Maximum => Volatile.Read(ref _maximum);

    internal void Enter()
    {
        int current = Interlocked.Increment(ref _active);
        int seen = Volatile.Read(ref _maximum);
        while (current > seen)
        {
            int prior = Interlocked.CompareExchange(ref _maximum, current, seen);
            if (prior == seen)
            {
                break;
            }

            seen = prior;
        }
    }

    internal void Exit() => Interlocked.Decrement(ref _active);
}
