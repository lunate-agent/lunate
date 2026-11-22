using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// Reads a background bash job's unread output and state. The job registry is the session's, so
/// only jobs this session started are visible; a job that ended keeps its final unread output for
/// the poll that reports its exit code.
/// </summary>
internal sealed class BashOutputTool(BashJobRegistry jobs) : ITool
{
    internal const string ToolName = "bash_output";

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
        "Read the output a background bash job produced since the last read, and whether it is still running or has ended with its exit code.";

    public JsonElement ParametersSchema => Schema;

    public ToolRisk Risk => ToolRisk.ReadOnly;

    public async Task<ToolResult> ExecuteAsync(
        JsonElement args,
        ToolContext ctx,
        CancellationToken ct
    )
    {
        if (!TryJobId(args, out string? id, out string? error))
        {
            return new ToolResult(error!, IsError: true);
        }

        if (jobs.Find(id!) is not { } job)
        {
            return new ToolResult(UnknownJob(id!), IsError: true);
        }

        ct.ThrowIfCancellationRequested();
        BashJobRead read = await job.ReadAsync().ConfigureAwait(false);
        return new ToolResult(
            read.Output,
            read.IsError,
            read.ExitCode is { } exitCode ? new BashDetails(exitCode) : null
        );
    }

    internal static bool TryJobId(JsonElement args, out string? id, out string? error)
    {
        id = null;
        if (!ToolArguments.TryValidate(args, ["job_id"], out error))
        {
            return false;
        }

        if (!ToolArguments.TryString(args, "job_id", out string? value, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "job_id is required and must be a non-empty string";
            return false;
        }

        id = value;
        return true;
    }

    internal static string UnknownJob(string id) =>
        $"unknown background job '{id}'; start one with bash using background: true and pass the job id it returns";
}
