namespace Mermaid.Terminal;

internal sealed record SequenceParticipant(string Id, string Name);

internal abstract record SequenceStatement;

internal sealed record SequenceMessage(int From, int To, string Content, bool Dotted, bool Head)
    : SequenceStatement;

internal sealed record SequenceNote(string Content, int[] Over) : SequenceStatement;

internal sealed record SequenceModel(
    IReadOnlyList<SequenceParticipant> Participants,
    IReadOnlyList<SequenceStatement> Statements
);

/// <summary>
/// Parses <c>sequenceDiagram</c>s: participants declared with
/// <c>participant A as Long Name</c> or implied by first mention, messages over four arrows
/// (<c>-&gt;</c>, <c>--&gt;</c>, <c>-&gt;&gt;</c> and the historical <c>--&gt;&gt;</c>), and
/// <c>Note over A[ and B]: text</c>. Anything else is a parse failure that names its line.
/// </summary>
internal static class SequenceParser
{
    internal static MermaidDrawResult Draw(IReadOnlyList<string> source, MermaidGlyphSet glyphs) =>
        TryParse(source, out SequenceModel? model, out string? failure)
            ? MermaidDrawResult.Ok(SequenceDraw.Draw(model!, glyphs))
            : MermaidDrawResult.Fail(failure!);

    internal static bool TryParse(
        IReadOnlyList<string> source,
        out SequenceModel? model,
        out string? failure
    )
    {
        model = null;
        failure = null;
        if (source.All(static line => line.Trim().Length == 0))
        {
            failure = "the fence has no diagram";
            return false;
        }

        var seats = new List<SequenceParticipant>();
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var statements = new List<SequenceStatement>();
        for (var position = 0; position < source.Count; position++)
        {
            int number = position + 1;
            string statement = source[position].Trim();
            if (statement.Length == 0 || statement.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (!Statement(statement, number, seats, ids, statements, ref failure))
            {
                return false;
            }
        }

        model = new(seats.AsReadOnly(), statements.AsReadOnly());
        return true;
    }

    private static bool Statement(
        string statement,
        int number,
        List<SequenceParticipant> seats,
        Dictionary<string, int> ids,
        List<SequenceStatement> statements,
        ref string? failure
    )
    {
        if (ids.Count == 0 && statement.Equals("sequenceDiagram", StringComparison.Ordinal))
        {
            return true;
        }

        string word = FirstWord(statement);
        switch (word)
        {
            case "sequenceDiagram" when statement.Length > word.Length:
                failure = $"line {number}: the header cannot carry a statement";
                return false;
            case "sequenceDiagram":
                return true;
            case "actor":
                failure = $"line {number}: unsupported statement '{word}'";
                return false;
            case "participant":
                return Participant(statement, number, seats, ids, ref failure);
            case "Note":
                return Note(statement, number, seats, ids, statements, ref failure);
            default:
                return Message(statement, number, seats, ids, statements, ref failure);
        }
    }

    private static bool Participant(
        string statement,
        int number,
        List<SequenceParticipant> seats,
        Dictionary<string, int> ids,
        ref string? failure
    )
    {
        string rest = statement["participant".Length..].Trim();
        int space = rest.IndexOf(' ', StringComparison.Ordinal);
        string id = space < 0 ? rest : rest[..space];
        string tail = space < 0 ? string.Empty : rest[(space + 1)..];
        if (id.Length == 0)
        {
            failure = $"line {number}: a participant needs an actor: participant <id> as <name>";
            return false;
        }

        string name;
        if (tail.Equals("as", StringComparison.Ordinal))
        {
            failure = $"line {number}: a participant needs a display name after 'as'";
            return false;
        }

        if (tail.StartsWith("as ", StringComparison.Ordinal))
        {
            name = Unquote(tail["as ".Length..].Trim());
            if (name.Length == 0)
            {
                failure = $"line {number}: a participant needs a display name after 'as'";
                return false;
            }
        }
        else
        {
            name = tail.Length > 0 ? Unquote(tail) : id;
        }

        if (!ids.ContainsKey(id))
        {
            ids[id] = seats.Count;
            seats.Add(new SequenceParticipant(id, name));
        }

        return true;
    }

    private static bool Note(
        string statement,
        int number,
        List<SequenceParticipant> seats,
        Dictionary<string, int> ids,
        List<SequenceStatement> statements,
        ref string? failure
    )
    {
        string rest = statement["Note".Length..].Trim();
        if (!rest.StartsWith("over", StringComparison.Ordinal))
        {
            failure = $"line {number}: unsupported statement 'Note {FirstWord(rest)}'";
            return false;
        }

        rest = rest["over".Length..].Trim();
        int at = rest.IndexOf(':', StringComparison.Ordinal);
        if (at < 0)
        {
            failure = $"line {number}: a note needs its text after ':'";
            return false;
        }

        var over = new List<int>();
        foreach (string raw in rest[..at].Split(','))
        {
            string id = raw.Trim();
            if (id.Length == 0 || ids.Count < 1 || !ids.TryGetValue(id, out int seat))
            {
                ids[id] = seats.Count;
                seats.Add(new SequenceParticipant(id, id));
                seat = ids[id];
            }

            over.Add(seat);
        }

        string content = Unquote(rest[(at + 1)..].Trim());
        if (content.Length == 0)
        {
            failure = $"line {number}: a note needs its text after ':'";
            return false;
        }

        statements.Add(new SequenceNote(content, [.. over]));
        return true;
    }

    private static bool Message(
        string statement,
        int number,
        List<SequenceParticipant> seats,
        Dictionary<string, int> ids,
        List<SequenceStatement> statements,
        ref string? failure
    )
    {
        int arrow = FirstArrow(statement, out int length, out bool dotted, out bool head);
        if (arrow < 0)
        {
            failure = $"line {number}: unsupported statement '{FirstWord(statement)}'";
            return false;
        }

        string fromId = statement[..arrow].Trim();
        string tail = statement[(arrow + length)..].Trim();
        int at = tail.IndexOf(':', StringComparison.Ordinal);
        string toId = (at < 0 ? tail : tail[..at]).Trim();
        if (!Participant(fromId, seats, ids) || !Participant(toId, seats, ids))
        {
            failure = $"line {number}: I cannot read '{FirstWord(statement[..arrow])}'";
            return false;
        }

        if (ids[fromId] == ids[toId])
        {
            failure = $"line {number}: a message needs two different actors";
            return false;
        }

        string content = at < 0 ? string.Empty : Unquote(tail[(at + 1)..].Trim());
        statements.Add(new SequenceMessage(ids[fromId], ids[toId], content, dotted, head));
        return true;
    }

    private static int FirstArrow(string statement, out int length, out bool dotted, out bool head)
    {
        length = 0;
        dotted = false;
        head = false;
        (string Text, int Length, bool Dotted, bool Head)[] arrows =
        [
            ("-->>", 4, true, true),
            ("->>", 3, false, true),
            ("-->", 3, true, false),
            ("->", 2, false, false),
        ];
        foreach ((string text, int span, bool mark, bool pointed) in arrows)
        {
            int at = statement.IndexOf(text, StringComparison.Ordinal);
            if (at > 0)
            {
                length = span;
                dotted = mark;
                head = pointed;
                return at;
            }
        }

        return -1;
    }

    private static bool Participant(
        string id,
        List<SequenceParticipant> seats,
        Dictionary<string, int> ids
    )
    {
        if (!id.All(static letter => char.IsLetterOrDigit(letter) || letter is '_' or '-'))
        {
            return false;
        }

        if (!ids.TryGetValue(id, out int index))
        {
            index = seats.Count;
            ids[id] = index;
            seats.Add(new SequenceParticipant(id, id));
        }

        return true;
    }

    private static string FirstWord(string text)
    {
        int end = text.IndexOf(' ', StringComparison.Ordinal);
        return end > 0 ? text[..end] : text;
    }

    private static string Unquote(string text) =>
        text.Length > 1 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;
}
