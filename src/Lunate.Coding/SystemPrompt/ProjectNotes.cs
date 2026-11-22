using System.Globalization;
using System.Text;

namespace Lunate.Coding;

/// <summary>
/// The project notes file, <c>LUNATE.md</c>: a readable place for facts the agent carries from one
/// session to the next. It is discovered at the repository root (the worktree root when the
/// repository root is not an ancestor of it, as <see cref="AgentsInstructions"/> falls back), read
/// after the <c>AGENTS.md</c> chain so its facts never outrank a rule, and bounded to
/// <see cref="MaxCharacters"/> so one file cannot fill the prompt.
/// </summary>
internal static class ProjectNotes
{
    /// <summary>The notes file name.</summary>
    internal const string FileName = "LUNATE.md";

    /// <summary>The most characters the file may hold and the prompt may carry.</summary>
    internal const int MaxCharacters = 8_000;

    /// <summary>The heading that introduces the notes in the system prompt.</summary>
    internal const string PromptHeading = "### Project notes (LUNATE.md)";

    private const string Precedence =
        "These are facts from earlier sessions; the AGENTS.md rules above win if they disagree.";

    /// <summary>The directory the notes file belongs to.</summary>
    internal static string Root(Workspace workspace)
    {
        string repositoryRoot = workspace.RepoRoot ?? workspace.WorktreeRoot;
        return Workspace.IsInside(repositoryRoot, workspace.WorktreeRoot)
            ? repositoryRoot
            : workspace.WorktreeRoot;
    }

    /// <summary>The notes file path in the repository (or worktree) root.</summary>
    internal static string PathFor(Workspace workspace) => Path.Combine(Root(workspace), FileName);

    /// <summary>
    /// The prompt block, or an empty string when the file is missing or empty. Content over the
    /// budget keeps its first <see cref="MaxCharacters"/> characters followed by one marker naming
    /// the omitted count that never splits a surrogate pair.
    /// </summary>
    internal static string Compose(Workspace workspace)
    {
        string path = PathFor(workspace);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        string content = File.ReadAllText(path).TrimEnd();
        if (content.Length == 0)
        {
            return string.Empty;
        }

        int shownLength = Math.Min(content.Length, MaxCharacters);
        if (
            shownLength < content.Length
            && shownLength > 0
            && char.IsHighSurrogate(content[shownLength - 1])
            && char.IsLowSurrogate(content[shownLength])
        )
        {
            shownLength--;
        }

        var block = new StringBuilder();
        block.Append(PromptHeading).Append("\n\n");
        block.Append(Precedence).Append("\n\n");
        block.Append(content.AsSpan(0, shownLength));
        if (shownLength < content.Length)
        {
            int omitted = content.Length - shownLength;
            block
                .Append("\n\n[notes truncated: ")
                .Append(omitted.ToString(CultureInfo.InvariantCulture))
                .Append(" characters omitted; read ")
                .Append(RelativePath(workspace, path))
                .Append(" for the rest]");
        }

        return block.ToString();
    }

    private static string RelativePath(Workspace workspace, string path) =>
        Path.GetRelativePath(workspace.WorktreeRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
}
