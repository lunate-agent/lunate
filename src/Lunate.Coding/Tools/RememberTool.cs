using System.Globalization;
using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// The core <c>remember</c> tool: one durable project fact appended to the repository's own
/// <c>LUNATE.md</c>. It takes no path, so it cannot write global or user state, and it never
/// rewrites an existing line; the only edit it makes to an existing file is the final line ending
/// the new bullet needs. The file's size budget is explicit and a refused append leaves its bytes
/// untouched.
/// </summary>
public sealed class RememberTool(
    Workspace workspace,
    IFileMutationQueue? mutations = null,
    ITextFileAccess? files = null
) : ITool
{
    /// <summary>The model-facing name.</summary>
    internal const string ToolName = "remember";

    private static readonly IReadOnlyList<string> Accepted = ["note"];

    private readonly IFileMutationQueue _mutations = mutations ?? FileMutationQueue.Shared;

    private readonly ITextFileAccess _files = files ?? LocalTextFileAccess.Instance;

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "note": {
                  "type": "string",
                  "description": "One durable project fact to append to LUNATE.md, on a single line"
                }
              },
              "required": ["note"]
            }
            """
        )
        .RootElement.Clone();

    public string Name => ToolName;

    public string Description =>
        "Append one durable project fact to the repository's LUNATE.md. Later sessions read the "
        + "file from the system prompt. The file is capped at 8,000 characters; keep each note to "
        + "one line, and use edit when a stale note must be removed.";

    public JsonElement ParametersSchema => Schema;

    public ToolRisk Risk => ToolRisk.Write;

    public async Task<ToolResult> ExecuteAsync(
        JsonElement args,
        ToolContext ctx,
        CancellationToken ct
    )
    {
        if (!TryParse(args, out string? note, out string? error))
        {
            return Error(error!);
        }

        string root = ProjectNotes.Root(workspace);
        string target = Workspace.Canonicalize(ProjectNotes.PathFor(workspace));
        if (!Workspace.IsInside(root, target))
        {
            return Error(
                $"{ProjectNotes.FileName} resolves outside the project root; move the link's target "
                    + "inside the repository and try again."
            );
        }

        return await _mutations.RunAsync(
            target,
            mutationCt =>
            {
                _ = mutationCt;
                bool created;
                string oldText;
                string ending;
                bool hasBom;
                try
                {
                    created = !_files.Exists(target);
                    if (created)
                    {
                        oldText = string.Empty;
                        ending = LineEndingPolicy.ResolveEnding(target);
                        hasBom = LineEndingPolicy.ResolveBom(target);
                    }
                    else
                    {
                        (oldText, hasBom) = _files.ReadRaw(target);
                        ending = ExistingEnding(oldText, target);
                    }
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    return Task.FromResult(Error($"could not be read: {exception.Message}"));
                }

                string bullet = "- " + note;
                string written;
                if (created)
                {
                    written = "# Project notes" + ending + ending + bullet + ending;
                }
                else
                {
                    string separator = EndsWithLineEnding(oldText) ? string.Empty : ending;
                    written = oldText + separator + bullet + ending;
                }

                if (written.Length > ProjectNotes.MaxCharacters)
                {
                    string budget = ProjectNotes.MaxCharacters.ToString(
                        "N0",
                        CultureInfo.InvariantCulture
                    );
                    return Task.FromResult(
                        Error(
                            $"{ProjectNotes.FileName} holds "
                                + $"{oldText.Length.ToString("N0", CultureInfo.InvariantCulture)} of "
                                + $"{budget} characters; this note would make it "
                                + $"{written.Length.ToString("N0", CultureInfo.InvariantCulture)}. "
                                + "Shorten the note, or edit the file to remove stale notes; the "
                                + "file was not changed."
                        )
                    );
                }

                try
                {
                    if (created)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    }

                    _files.WriteRaw(target, written, hasBom);
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    return Task.FromResult(Error($"could not be written: {exception.Message}"));
                }

                string relative = RelativePath(ctx.WorkingDirectory, target);
                ctx.FileChanges.Notify(
                    new FileChangeEvent(
                        target,
                        relative,
                        ctx.WorkingDirectory,
                        ctx.RunId,
                        created ? FileChangeKind.Created : FileChangeKind.Modified
                    )
                );
                AgentToolContextState.ReadsFor(ctx)?.Refresh(target, written, relative);

                string size = written.Length.ToString("N0", CultureInfo.InvariantCulture);
                string cap = ProjectNotes.MaxCharacters.ToString(
                    "N0",
                    CultureInfo.InvariantCulture
                );
                return Task.FromResult(
                    Result(
                        $"remembered in {ProjectNotes.FileName} ({(created ? "created" : "updated")}; "
                            + $"{size} of {cap} characters); later sessions read it from the system prompt"
                    )
                );
            },
            ct
        );
    }

    /// <summary>The one validator shared by every call: one non-empty, single-line <c>note</c>.</summary>
    internal static bool TryParse(JsonElement args, out string? note, out string? error)
    {
        note = null;
        if (!ToolArguments.TryValidate(args, Accepted, out error))
        {
            return false;
        }

        if (
            !args.TryGetProperty("note", out JsonElement noteElement)
            || noteElement.ValueKind != JsonValueKind.String
        )
        {
            error = "note is required and must be a non-empty single-line string";
            return false;
        }

        string value = noteElement.GetString()!.Trim();
        if (value.Length == 0)
        {
            error = "note is required and must be a non-empty single-line string";
            return false;
        }

        if (value.Contains('\n') || value.Contains('\r'))
        {
            error = "note must be a single line; append one fact per call";
            return false;
        }

        note = value;
        return true;
    }

    /// <summary>
    /// The ending the existing file's own text uses, whatever a policy above it says: its dominant
    /// LF or CRLF ending, a CR-only file's CR, and the project convention only for a text that
    /// carries no line ending at all.
    /// </summary>
    private static string ExistingEnding(string text, string path) =>
        text.Contains('\n') ? TextFile.DominantEnding(text)
        : text.Contains('\r') ? "\r"
        : LineEndingPolicy.ResolveEnding(path);

    /// <summary>
    /// Whether the text already ends with a line ending, so the new bullet needs no separator.
    /// A lone CR is the ending of a CR-only file, not a character in the middle of a line.
    /// </summary>
    private static bool EndsWithLineEnding(string text) =>
        text.EndsWith('\n') || text.EndsWith('\r');

    private static string RelativePath(string workingDirectory, string target) =>
        Path.GetRelativePath(workingDirectory, target)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static ToolResult Result(string output) => new(output, IsError: false);

    private static ToolResult Error(string output) => new(output, IsError: true);
}
