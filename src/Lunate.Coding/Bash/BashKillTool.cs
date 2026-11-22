using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// Stops one background bash job and its whole process tree with the same children-first kill the
/// foreground path uses. A job that already ended reports its exit code instead of a fake kill.
/// </summary>
internal sealed class BashKillTool(BashJobRegistry jobs) : ITool
{
    internal const string ToolName = "bash_kill";

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "job_id": {
                  "type": "string",
                  "description": "The job id returned by bash with background: true"
                }
              },
              "required": ["job_id"]
            }
            """
        )
        .RootElement.Clone();

    public string Name => ToolName;

    public string Description =>
        "Stop a background bash job and its whole process tree. A job that already ended reports its exit code.";

    public JsonElement ParametersSchema => Schema;

    public ToolRisk Risk => ToolRisk.Execute;

    public async Task<ToolResult> ExecuteAsync(
        JsonElement args,
        ToolContext ctx,
        CancellationToken ct
    )
    {
        if (!BashOutputTool.TryJobId(args, out string? id, out string? error))
        {
            return new ToolResult(error!, IsError: true);
        }

        if (jobs.Find(id!) is not { } job)
        {
            return new ToolResult(BashOutputTool.UnknownJob(id!), IsError: true);
        }

        ct.ThrowIfCancellationRequested();
        await job.KillAsync().ConfigureAwait(false);
        BashJobRead read = await job.ReadAsync().ConfigureAwait(false);
        return new ToolResult(
            read.Output,
            IsError: false,
            read.ExitCode is { } exitCode ? new BashDetails(exitCode) : null
        );
    }
}
