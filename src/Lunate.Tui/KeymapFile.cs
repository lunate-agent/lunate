using System.Text.Json;

namespace Lunate.Tui;

/// <summary>
/// The user keymap file — <c>~/.lunate/keymap.json</c>, the catalogue's overlay. Same top-level
/// shape as <c>keys.json</c> (<c>version</c>, <c>bindings</c>); each binding names the id it
/// rebinds and the keys it answers with, and nothing else: the intent, the context and the
/// description stay the catalogue's. The path is chosen once: the <c>--keymap</c> flag over the
/// <c>LUNATE_KEYMAP</c> environment variable over <c>keymap.json</c> in the Lunate home. A missing
/// file leaves the catalogue alone; a file that cannot be read, parsed or applied fails the load
/// with one line, and nothing of it is applied.
/// </summary>
internal static class KeymapFile
{
    internal const string FileName = "keymap.json";
    internal const string Variable = "LUNATE_KEYMAP";

    /// <summary>
    /// The file this run reads: <paramref name="flagPath"/> when <c>--keymap</c> named one, else
    /// <paramref name="environment"/>'s <see cref="Variable"/>, else <c>keymap.json</c> under
    /// <paramref name="home"/>. The resolver takes the environment function instead of reading the
    /// process so a test states both sides of the precedence and a session's own environment seam
    /// is honoured.
    /// </summary>
    internal static string ResolvePath(
        string? flagPath,
        string home,
        Func<string, string?>? environment = null
    ) =>
        NonEmpty(flagPath)
        ?? NonEmpty((environment ?? Environment.GetEnvironmentVariable)(Variable))
        ?? Path.Combine(home, FileName);

    /// <summary>
    /// Reads and applies <paramref name="path"/>. A file that fails anywhere — unreadable,
    /// unparsable, without a <c>bindings</c> array, or with a malformed binding, an unknown id, an
    /// id given twice, or two of its own bindings in conflict — fails the whole load: the returned
    /// error names why, and the catalogue keeps its shipped keys.
    /// </summary>
    internal static KeymapOutcome Apply(string path)
    {
        if (!File.Exists(path))
        {
            return KeymapOutcome.Applied([]);
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return KeymapOutcome.Failed($"keymap: {path}: {exception.Message}");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return KeymapOutcome.Failed(
                $"keymap: {path} is not readable JSON: {exception.Message}"
            );
        }

        try
        {
            using (document)
            {
                if (
                    !document.RootElement.TryGetProperty("bindings", out JsonElement bindings)
                    || bindings.ValueKind != JsonValueKind.Array
                )
                {
                    return KeymapOutcome.Failed(
                        $"keymap: {path} has no 'bindings' array, so no binding in it applies"
                    );
                }

                return KeyBindingCatalog.Overlay(Entries(path, bindings));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return KeymapOutcome.Failed($"keymap: {path}: {exception.Message}");
        }
        catch (KeymapJsonException exception)
        {
            return KeymapOutcome.Failed(exception.Message);
        }
    }

    /// <summary>
    /// The file's bindings, each the id and keys its entry names. A malformed binding throws: the
    /// message names the binding and what is wrong with it, and the load fails wholesale.
    /// </summary>
    private static List<UserKeymapEntry> Entries(string path, JsonElement bindings)
    {
        var entries = new List<UserKeymapEntry>();
        foreach (JsonElement binding in bindings.EnumerateArray())
        {
            entries.Add(Parse(path, binding));
        }

        return entries;
    }

    private static UserKeymapEntry Parse(string path, JsonElement binding)
    {
        if (
            !binding.TryGetProperty("id", out JsonElement id)
            || id.ValueKind != JsonValueKind.String
            || id.GetString() is not { Length: > 0 } name
        )
        {
            throw new KeymapJsonException(
                $"keymap: {path}: a binding entry has no 'id' string, so the load cannot tell which "
                    + "binding it means"
            );
        }

        if (
            !binding.TryGetProperty("keys", out JsonElement keys)
            || keys.ValueKind != JsonValueKind.Array
        )
        {
            throw new KeymapJsonException(
                $"keymap: {path}: binding '{name}' has no 'keys' array, so it can never answer a key"
            );
        }

        var patterns = new List<KeyPattern>();
        foreach (JsonElement key in keys.EnumerateArray())
        {
            if (!TryPattern(key, out KeyPattern pattern, out string? problem))
            {
                throw new KeymapJsonException($"keymap: {path}: binding '{name}' has {problem}");
            }

            patterns.Add(pattern);
        }

        return new UserKeymapEntry(name, patterns);
    }

    private static bool TryPattern(JsonElement key, out KeyPattern pattern, out string? error)
    {
        pattern = default;
        if (key.ValueKind != JsonValueKind.Object)
        {
            error =
                "a key that is not an object: { \"kind\", \"ctrl\", \"alt\", \"shift\", \"text\" }";
            return false;
        }

        if (
            !key.TryGetProperty("kind", out JsonElement kind)
            || kind.ValueKind != JsonValueKind.String
            || !Enum.TryParse(kind.GetString(), ignoreCase: false, out KeyKind parsed)
            || !Enum.IsDefined(parsed)
        )
        {
            error = "a 'kind' that is not a KeyKind member";
            return false;
        }

        pattern = new KeyPattern(
            parsed,
            Modifier(key, "ctrl"),
            Modifier(key, "alt"),
            Modifier(key, "shift"),
            key.TryGetProperty("text", out JsonElement text) ? text.GetString() : null
        );
        error = null;
        return true;
    }

    private static bool? Modifier(JsonElement key, string name) =>
        key.TryGetProperty(name, out JsonElement value) ? value.GetBoolean() : null;

    private static string? NonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// A user keymap file whose shape the reader cannot accept, carrying the one-line failure /
/// message the caller shows; the load of the file that threw it does not apply.
/// </summary>
internal sealed class KeymapJsonException(string message) : Exception(message);
