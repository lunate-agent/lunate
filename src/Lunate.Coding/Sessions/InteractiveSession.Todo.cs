using Lunate.Agent;

namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    /// <summary>
    /// Shows the session's recorded todo list after a harness rebuild (/resume, /new, /model and
    /// the startup load). A fresh or fileless session clears the block, so no list leaks across
    /// sessions.
    /// </summary>
    private void ApplyTodoReplay()
    {
        IReadOnlyList<TodoItem>? items = _session is null ? null : TodoReplay.FromSession(_session);
        _live.SetTodo(items is null ? null : TodoReplay.ToView(items));
    }
}
