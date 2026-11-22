namespace Lunate.Tui;

/// <summary>The status a todo item shows; the glyph carries it, never colour alone.</summary>
internal enum TodoItemState
{
    Pending,
    InProgress,
    Done,
}

/// <summary>One live-area todo line: the model's text and its status.</summary>
internal readonly record struct TodoItemView(string Text, TodoItemState Status);
