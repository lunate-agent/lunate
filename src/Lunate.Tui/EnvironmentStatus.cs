namespace Lunate.Tui;

/// <summary>
/// The resolved execution-environment fact shared by the context command and the welcome header.
/// <see cref="State"/> is a short readiness word (<c>starting</c>, <c>ready</c>, or
/// <c>failed: …</c>); reporting it never creates a container.
/// </summary>
internal sealed record EnvironmentStatus(string Engine, string Image, string State)
{
    /// <summary>The selection in the user's vocabulary, <c>engine:image</c>.</summary>
    internal string Display => $"{Engine}:{Image}";

    /// <summary>The header segment; ASCII punctuation when the terminal lacks the middot.</summary>
    internal string HeaderText(bool unicode) => string.Concat("environment ", Display);

    /// <summary>The explicit <c>/context</c> segment, which names the readiness state too.</summary>
    internal string ContextText => $"environment: {Display} ({State})";
}
