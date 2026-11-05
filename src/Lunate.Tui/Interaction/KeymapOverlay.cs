namespace Lunate.Tui;

/// <summary>
/// One entry of a user keymap file: the binding id and the keys the binding answers with after the
/// file is applied — an empty list disabling it. Only <c>keys</c> changes; the binding's intent,
/// context and description stay the catalogue's, because intents are code a file cannot invent.
/// </summary>
internal sealed record UserKeymapEntry(string Id, IReadOnlyList<KeyPattern> Keys);

/// <summary>
/// The result of applying a user keymap file: an error that fails the whole load (nothing was
/// applied), or the conflict lines the caller prints at startup after the file was applied.
/// </summary>
internal sealed record KeymapOutcome(string? Error, IReadOnlyList<string> ConflictLines)
{
    public static KeymapOutcome Applied(IReadOnlyList<string> conflictLines) =>
        new(Error: null, ConflictLines: conflictLines);

    public static KeymapOutcome Failed(string error) => new(Error: error, ConflictLines: []);
}

/// <summary>
/// The user keymap file, applied to the catalogue by binding id. It decides the whole file's
/// effect before committing any of it: an unknown id, an id given twice, or two of the file's own
/// bindings answering the same key fails the load and leaves the catalogue exactly as it was — a
/// session never starts with a keymap it could not apply, because a silently ignored binding is a
/// user pressing a key that does nothing. Where one of the file's bindings and a catalogue binding
/// the file does not touch answer the same key in a context they share, the file wins: the
/// catalogue binding loses that key, and the caller gets one line naming both ids.
/// </summary>
internal static partial class KeyBindingCatalog
{
    internal static KeymapOutcome Overlay(IReadOnlyList<UserKeymapEntry> entries)
    {
        lock (Gate)
        {
            string? loadFailure = Validate(
                entries,
                out Dictionary<string, List<KeyPattern>>? fileSets
            );
            if (loadFailure is not null || fileSets is null)
            {
                return KeymapOutcome.Failed(
                    loadFailure ?? "keymap: the file could not decide what it changes"
                );
            }

            List<(string Id, List<KeyBinding> Rows)> groups = Groups();
            Dictionary<string, HashSet<KeyBinding>> removed = new(StringComparer.Ordinal);
            var conflicts = new List<string>();
            foreach ((string fileId, List<KeyBinding> _) in groups)
            {
                if (!fileSets.ContainsKey(fileId))
                {
                    continue;
                }

                string? judged = Judge(fileId, fileSets, groups, removed, conflicts);
                if (judged is not null)
                {
                    return KeymapOutcome.Failed(judged);
                }
            }

            All = Rebuild(fileSets, removed);
            return KeymapOutcome.Applied(conflicts);
        }
    }

    /// <summary>
    /// One file binding's effect on every other id, evaluated before anything lands: two of the
    /// file's own bindings that answer the same key fail the load — a typo, not a preference; a
    /// catalogue binding that did not lose the key keeps its row, and one that did loses it here
    /// and contributes one conflict line.
    /// </summary>
    private static string? Judge(
        string fileId,
        IReadOnlyDictionary<string, List<KeyPattern>> fileSets,
        List<(string Id, List<KeyBinding> Rows)> groups,
        Dictionary<string, HashSet<KeyBinding>> removed,
        List<string> conflicts
    )
    {
        IReadOnlyList<KeyBinding> anchorRows = BindingsFor(fileId);
        KeyContext contexts = anchorRows[0].Contexts;
        List<KeyPattern> patterns = fileSets[fileId];
        foreach ((string ownerId, List<KeyBinding> rows) in groups)
        {
            if (string.Equals(ownerId, fileId, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (KeyPattern pattern in patterns)
            {
                List<KeyBinding> claimed = Lost(rows, removed, pattern, contexts);
                if (claimed.Count == 0)
                {
                    continue;
                }

                if (fileSets.TryGetValue(ownerId, out List<KeyPattern>? _))
                {
                    return $"keymap: two of the file's bindings answer "
                        + $"{KeyPatternFormatter.Describe(pattern)}: '{fileId}' and '{ownerId}'; "
                        + "that is a typo, not a preference";
                }

                if (!removed.TryGetValue(ownerId, out HashSet<KeyBinding>? lost))
                {
                    lost = new HashSet<KeyBinding>();
                    removed[ownerId] = lost;
                }

                lost.UnionWith(claimed);
                conflicts.Add(
                    $"keymap: {fileId} and {ownerId} both answer "
                        + $"{KeyPatternFormatter.Describe(pattern)} in context "
                        + $"{ContextOverlapText(contexts, claimed[0].Contexts)}; {fileId} wins"
                );
            }
        }

        return null;
    }

    /// <summary>
    /// An id's rows that still answer one of the candidate's patterns in a context both apply to,
    /// minus the rows an earlier file entry already took from it.
    /// </summary>
    private static List<KeyBinding> Lost(
        List<KeyBinding> rows,
        IReadOnlyDictionary<string, HashSet<KeyBinding>> removed,
        KeyPattern pattern,
        KeyContext contexts
    )
    {
        HashSet<KeyBinding>? gone = removed.TryGetValue(rows[0].Id, out HashSet<KeyBinding>? value)
            ? value
            : null;
        return
        [
            .. rows.Where(row =>
                    Overlaps(row.Key, pattern) && ContextsOverlap(row.Contexts, contexts)
                )
                .Where(row => gone is null || !gone.Contains(row)),
        ];
    }

    private static List<(string Id, List<KeyBinding> Rows)> Groups()
    {
        var groups = new Dictionary<string, List<KeyBinding>>(StringComparer.Ordinal);
        foreach (KeyBinding row in All)
        {
            if (!groups.TryGetValue(row.Id, out List<KeyBinding>? rows))
            {
                rows = [];
                groups[row.Id] = rows;
            }

            rows.Add(row);
        }

        return [.. groups.Select(group => (group.Key, group.Value))];
    }

    /// <summary>
    /// The ids the file does not touch name their own contexts only when the contexts cross: the
    /// file binding's own context when the other row is unconstrained, the other way around too,
    /// the shared contexts when both constrain; a pair that shares none is not a conflict.
    /// </summary>
    private static string ContextOverlapText(KeyContext file, KeyContext catalogue)
    {
        KeyContext overlap =
            file == KeyContext.None ? catalogue
            : catalogue == KeyContext.None ? file
            : file & catalogue;
        return overlap == KeyContext.None
            ? "every context"
            : string.Join(
                ", ",
                Enum.GetValues<KeyContext>()
                    .Where(value => value != KeyContext.None && (overlap & value) == value)
                    .Select(value => value.ToString())
            );
    }

    private static string? Validate(
        IReadOnlyList<UserKeymapEntry> entries,
        out Dictionary<string, List<KeyPattern>>? fileSets
    )
    {
        fileSets = new Dictionary<string, List<KeyPattern>>(StringComparer.Ordinal);
        foreach (UserKeymapEntry entry in entries)
        {
            if (!fileSets.TryAdd(entry.Id, [.. entry.Keys]))
            {
                return $"keymap: '{entry.Id}' is given twice; a file maps each binding once";
            }
        }

        foreach (string id in fileSets.Keys)
        {
            if (BindingsFor(id).Count > 0)
            {
                continue;
            }

            IReadOnlyList<string> nearest = SimilarIds(id);
            return nearest.Count == 0
                ? $"keymap: '{id}' is not a binding id"
                : $"keymap: '{id}' is not a binding id; the closest names are "
                    + string.Join(", ", nearest.Select(name => $"'{name}'"));
        }

        return null;
    }

    /// <summary>
    /// The catalogue under one file's overlay: each of the file's ids has its rows replaced by its
    /// entry's keys at the position their original rows held (an empty entry removes them — the
    /// disabled binding), every other id keeps the rows the conflict resolution left it.
    /// </summary>
    private static IReadOnlyList<KeyBinding> Rebuild(
        IReadOnlyDictionary<string, List<KeyPattern>> fileSets,
        IReadOnlyDictionary<string, HashSet<KeyBinding>> removed
    )
    {
        List<(string Id, List<KeyBinding> Rows)> groups = Groups();
        var rebuilt = new List<KeyBinding>(All.Count);
        foreach ((string id, List<KeyBinding> rows) in groups)
        {
            if (fileSets.TryGetValue(id, out List<KeyPattern>? patterns))
            {
                rebuilt.AddRange(Replaced(id, rows, patterns!));
                continue;
            }

            rebuilt.AddRange(
                removed.TryGetValue(id, out HashSet<KeyBinding>? lost)
                    ? rows.Where(row => !lost.Contains(row))
                    : rows
            );
        }

        return rebuilt;
    }

    /// <summary>One id's rows with a user entry's keys in place of the catalogue's own.</summary>
    private static List<KeyBinding> Replaced(
        string id,
        IReadOnlyList<KeyBinding> originals,
        IReadOnlyList<KeyPattern> patterns
    )
    {
        if (patterns.Count == 0)
        {
            return [];
        }

        KeyBinding anchor = originals[0];
        return
        [
            .. patterns.Select(pattern => new KeyBinding(
                id,
                pattern,
                anchor.Intent,
                anchor.Contexts,
                anchor.Description
            )),
        ];
    }

    /// <summary>
    /// Restores the catalogue to exactly these rows — the overlay's own undo, for the tests that
    /// mutate the static catalogue: a disabled binding cannot come back through
    /// <see cref="Remap"/>, which never accepts an empty key list.
    /// </summary>
    internal static void Replace(IReadOnlyList<KeyBinding> rows)
    {
        lock (Gate)
        {
            All = [.. rows];
        }
    }
}
