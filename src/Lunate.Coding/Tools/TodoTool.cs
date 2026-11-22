using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// The core <c>todo</c> tool: one short plan the model replaces whole on every call. The list is
/// session state, not a workspace mutation, so the tool is read-only and needs no approval. A valid
/// call emits one <see cref="TodoUpdated"/> carrying the complete list; the session's tool-call
/// message is what replays it on resume.
/// </summary>
public sealed class TodoTool : ITool
{
    /// <summary>The most items a "short list" may hold.</summary>
    internal const int MaxItems = 20;

    private static readonly IReadOnlyList<string> Accepted = ["items"];

    private static readonly IReadOnlyList<string> ItemAccepted = ["text", "status"];

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "items": {
                  "type": "array",
                  "maxItems": 20,
                  "description": "The complete list, replacing the previous one; an empty array clears it",
                  "items": {
                    "type": "object",
                    "properties": {
                      "text": {
                        "type": "string",
                        "description": "One short, concrete task"
                      },
                      "status": {
                        "type": "string",
                        "enum": ["pending", "in_progress", "done"],
                        "description": "pending, in_progress (at most one item) or done"
                      }
                    },
                    "required": ["text", "status"]
                  }
                }
              },
              "required": ["items"]
            }
            """
        )
        .RootElement.Clone();

    public string Name => "todo";

    public string Description =>
        "Set or replace the session's short todo list. Send the complete list every time, with "
        + "one status per item (pending, in_progress or done); at most one item may be in progress, "
        + "and an empty list clears the plan. Use it to keep a long task on track, not as a log.";

    public JsonElement ParametersSchema => Schema;

    public ToolRisk Risk => ToolRisk.ReadOnly;

    public ToolAnnotations Annotations => new(ReadOnly: true, Idempotent: true);

    public Task<ToolResult> ExecuteAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (!TryParse(args, out IReadOnlyList<TodoItem> items, out string? error))
        {
            return Task.FromResult(Error(error!));
        }

        if (ctx.RunId is { } runId)
        {
            ctx.Events.Emit(new TodoUpdated(runId, items));
        }

        return Task.FromResult(Result(Summary(items)));
    }

    /// <summary>
    /// The one validator shared by a live call and resume replay. It accepts a JSON object with only
    /// <c>items</c>, each item with only <c>text</c> and <c>status</c>, and returns the normalized list.
    /// </summary>
    internal static bool TryParse(
        JsonElement args,
        out IReadOnlyList<TodoItem> items,
        out string? error
    )
    {
        items = [];
        if (!ToolArguments.TryValidate(args, Accepted, out error))
        {
            return false;
        }

        if (
            !args.TryGetProperty("items", out JsonElement itemsElement)
            || itemsElement.ValueKind != JsonValueKind.Array
        )
        {
            error = "items is required and must be an array of {text, status} objects";
            return false;
        }

        List<TodoItem> parsed = [];
        int index = 0;
        foreach (JsonElement item in itemsElement.EnumerateArray())
        {
            string at = FormattableString.Invariant($"items[{index}]");
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                error = $"{at} must be an object with text and status";
                return false;
            }

            if (!ToolArguments.TryValidate(item, ItemAccepted, out string? unknown))
            {
                error = $"{at} {unknown}";
                return false;
            }

            if (
                !item.TryGetProperty("text", out JsonElement textElement)
                || textElement.ValueKind != JsonValueKind.String
            )
            {
                error = $"{at}.text is required and must be a non-empty string";
                return false;
            }

            string text = textElement.GetString()!.Trim();
            if (text.Length == 0)
            {
                error = $"{at}.text is required and must be a non-empty string";
                return false;
            }

            if (
                !item.TryGetProperty("status", out JsonElement statusElement)
                || statusElement.ValueKind != JsonValueKind.String
                || !TryStatus(statusElement.GetString(), out TodoItemStatus status)
            )
            {
                error = $"{at}.status must be 'pending', 'in_progress' or 'done'";
                return false;
            }

            parsed.Add(new TodoItem(text, status));
        }

        if (parsed.Count > MaxItems)
        {
            error = FormattableString.Invariant(
                $"items holds {parsed.Count} entries; keep the list to at most {MaxItems} items"
            );
            return false;
        }

        int inProgress = parsed.Count(item => item.Status is TodoItemStatus.InProgress);
        if (inProgress > 1)
        {
            error = FormattableString.Invariant(
                $"items marks {inProgress} items in_progress; mark at most one item in progress"
            );
            return false;
        }

        items = parsed;
        return true;
    }

    private static bool TryStatus(string? value, out TodoItemStatus status)
    {
        switch (value)
        {
            case "pending":
                status = TodoItemStatus.Pending;
                return true;
            case "in_progress":
                status = TodoItemStatus.InProgress;
                return true;
            case "done":
                status = TodoItemStatus.Done;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static string Summary(IReadOnlyList<TodoItem> items)
    {
        if (items.Count == 0)
        {
            return "todo list cleared";
        }

        int pending = items.Count(item => item.Status is TodoItemStatus.Pending);
        int inProgress = items.Count(item => item.Status is TodoItemStatus.InProgress);
        int done = items.Count(item => item.Status is TodoItemStatus.Done);
        return FormattableString.Invariant(
            $"todo list updated: {items.Count} items ({pending} pending, {inProgress} in progress, {done} done)"
        );
    }

    private static ToolResult Result(string output) => new(output, IsError: false);

    private static ToolResult Error(string output) => new(output, IsError: true);
}
