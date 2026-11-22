using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

public sealed partial class BashBackgroundJobTests
{
    private static async Task<(string Id, ToolResult Start)> StartAsync(
        BashTool tool,
        string command
    )
    {
        ToolResult start = await tool.ExecuteAsync(
            Args(JsonSerializer.Serialize(new { command, background = true })),
            Context,
            CancellationToken.None
        );
        Assert.False(start.IsError, start.Output);
        const string marker = "started background job ";
        int begin = start.Output.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        int end = start.Output.IndexOf(';', begin);
        Assert.True(begin >= marker.Length && end > begin, start.Output);
        return (start.Output[begin..end], start);
    }

    private static async Task<ToolResult> ReadUntilAsync(
        BashOutputTool tool,
        string id,
        Func<string, bool> done
    )
    {
        DateTime deadline = DateTime.UtcNow + WaitBudgets.SpawnWindow;
        var text = new System.Text.StringBuilder();
        ToolResult last;
        do
        {
            last = await tool.ExecuteAsync(JobArgs(id), Context, CancellationToken.None);
            text.Append(last.Output);
            if (done(text.ToString()))
            {
                return last with { Output = text.ToString() };
            }

            await Task.Delay(WaitBudgets.PollInterval);
        } while (DateTime.UtcNow < deadline);

        return last with
        {
            Output = text.ToString(),
        };
    }

    private static async Task WaitUntilEndedAsync(BashTool tool, string id)
    {
        DateTime deadline = DateTime.UtcNow + WaitBudgets.SpawnWindow;
        while (tool.Jobs.Find(id) is { IsRunning: true })
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"background job {id} did not end within the spawn window");
            }

            await Task.Delay(WaitBudgets.PollInterval);
        }
    }

    private static string TreeCommand() =>
        OperatingSystem.IsWindows()
            ? "powershell.exe -NoProfile -Command 'Set-Content -Path grandchild.pid -Value $PID; Start-Sleep 300' & sleep 300"
            : "sh -c 'echo $$ > grandchild.pid; exec sleep 300' & sleep 300";

    private static JsonElement JobArgs(string id) =>
        JsonSerializer.SerializeToElement(new { job_id = id });

    private static JsonElement Args(string arguments) =>
        JsonDocument.Parse(arguments).RootElement.Clone();

    private sealed class NullAgentEvents : IAgentEvents
    {
        public void Emit(AgentEvent agentEvent) { }
    }
}
