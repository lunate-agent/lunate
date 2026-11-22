using System.Text.Json;
using Lunate.Agent;
using Lunate.Tui;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// Reconstructs the session's todo list from the tool-call messages already stored on its active
/// path: the last valid <c>todo</c> call is the current list, and no new session entry is written.
/// The same parser validates a live call and a replayed one.
/// </summary>
internal static class TodoReplay
{
    /// <summary>The last valid todo list on the session's active path, or null when it has none.</summary>
    internal static IReadOnlyList<TodoItem>? FromSession(Session session)
    {
        IReadOnlyList<SessionEntry> path = SessionTree.Path(session.Entries, session.ActiveLeafId);
        return FromHistory(path.OfType<SessionMessageEntry>().Select(entry => entry.Message));
    }

    /// <summary>The last valid todo list in the given messages, or null when they have none.</summary>
    internal static IReadOnlyList<TodoItem>? FromHistory(IEnumerable<ChatMessage> history)
    {
        IReadOnlyList<TodoItem>? current = null;
        foreach (ChatMessage message in history)
        {
            foreach (AIContent content in message.Contents)
            {
                if (
                    content is not FunctionCallContent call
                    || !string.Equals(call.Name, "todo", StringComparison.Ordinal)
                )
                {
                    continue;
                }

                JsonElement arguments = JsonSerializer.SerializeToElement(
                    call.Arguments ?? new Dictionary<string, object?>()
                );
                if (TodoTool.TryParse(arguments, out IReadOnlyList<TodoItem> items, out _))
                {
                    current = items;
                }
            }
        }

        return current;
    }

    /// <summary>Maps the contract's list to the TUI's own small value type.</summary>
    internal static IReadOnlyList<TodoItemView> ToView(IReadOnlyList<TodoItem> items) =>
        [
            .. items.Select(item => new TodoItemView(
                item.Text,
                item.Status switch
                {
                    TodoItemStatus.InProgress => TodoItemState.InProgress,
                    TodoItemStatus.Done => TodoItemState.Done,
                    _ => TodoItemState.Pending,
                }
            )),
        ];
}
