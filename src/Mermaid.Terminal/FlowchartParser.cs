namespace Mermaid.Terminal;

internal sealed record FlowchartNode(string Id, IReadOnlyList<string> LabelLines);

internal sealed record FlowchartEdge(
    string From,
    string To,
    string? Label,
    bool Dashed,
    bool Arrowhead
);

/// <summary>
/// A parsed <c>flowchart</c>/<c>graph</c>: nodes in first-appearance order, edges in statement
/// order, and every node's layer (the longest path of edges reaching it). Layers strictly rise
/// along every edge, so a cycle is a parse failure and the drawing never has to loop.
/// </summary>
internal sealed record FlowchartModel(
    bool Horizontal,
    IReadOnlyList<FlowchartNode> Nodes,
    IReadOnlyList<FlowchartEdge> Edges,
    IReadOnlyList<int> Layers
);

/// <summary>
/// The bounded Mermaid flowchart grammar: node chains — <c>a[label] --&gt; b("text")</c> with
/// square, round, stadium and diamond shapes, quoted labels and <c>&lt;br/&gt;</c> breaks — and
/// edges with and without labels, plain (<c>---</c>) or dashed (<c>-. text .-&gt;</c>).
/// Subgraphs, styles, class definitions, click handlers and direction changes are parse failures;
/// the fence then falls back with the reason.
/// </summary>
internal static class FlowchartParser
{
    internal static MermaidDrawResult Draw(
        IReadOnlyList<string> source,
        string verb,
        MermaidGlyphSet glyphs
    ) =>
        TryParse(source, verb, out FlowchartModel? model, out string? failure)
            ? MermaidDrawResult.Ok(FlowchartDraw.Draw(model!, glyphs))
            : MermaidDrawResult.Fail(failure!);

    internal static bool TryParse(
        IReadOnlyList<string> source,
        string verb,
        out FlowchartModel? model,
        out string? failure
    )
    {
        model = null;
        failure = null;

        int header = 0;
        while (header < source.Count && source[header].Trim().Length == 0)
        {
            header++;
        }

        if (header == source.Count)
        {
            failure = "the fence has no diagram";
            return false;
        }

        string first = source[header].Trim();
        int line = header + 1;
        string direction = verb.Length < first.Length ? first[verb.Length..].Trim() : string.Empty;
        string canonical = direction.ToUpperInvariant();
        if (direction.Length == 0)
        {
            failure = $"line {line}: the diagram needs a direction (TD, TB or LR)";
            return false;
        }

        if (canonical is not ("TB" or "TD" or "LR"))
        {
            failure = $"line {line}: unsupported direction '{direction}'";
            return false;
        }

        var nodes = new List<FlowchartNode>();
        var seats = new Dictionary<string, int>(StringComparer.Ordinal);
        var edges = new List<FlowchartEdge>();
        for (var index = header + 1; index < source.Count; index++)
        {
            int number = index + 1;
            string statement = Statement(source[index]);
            if (statement.Length == 0 || statement.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            string word = FirstWord(statement);
            if (word is "subgraph" or "style" or "classDef" or "click")
            {
                failure = $"line {number}: unsupported statement '{word}'";
                return false;
            }

            var scanner = new Scanner(statement, number, seats, nodes, edges);
            if (!scanner.Run())
            {
                failure = scanner.Failure;
                return false;
            }
        }

        if (!Layers(edges, seats, nodes.Count, out int[] depth))
        {
            failure = "the diagram has a cycle";
            return false;
        }

        model = new(canonical == "LR", nodes, edges, depth);
        return true;
    }

    private static string Statement(string source)
    {
        string text = source.Trim();
        return text.EndsWith(';') ? text[..^1].TrimEnd() : text;
    }

    private static string FirstWord(string text)
    {
        int end = text.IndexOf(' ', StringComparison.Ordinal);
        return end > 0 ? text[..end] : text;
    }

    private static bool Layers(
        IReadOnlyList<FlowchartEdge> edges,
        Dictionary<string, int> seats,
        int nodeCount,
        out int[] depth
    )
    {
        depth = new int[nodeCount];
        for (var round = 0; round < nodeCount; round++)
        {
            var settled = true;
            foreach (FlowchartEdge edge in edges)
            {
                int source = seats[edge.From];
                int target = seats[edge.To];
                if (depth[target] <= depth[source])
                {
                    depth[target] = depth[source] + 1;
                    settled = false;
                }
            }

            if (settled)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One statement: a node, then any number of edges chaining nodes off it.</summary>
    private sealed class Scanner
    {
        private readonly string _text;
        private readonly int _line;
        private readonly Dictionary<string, int> _seats;
        private readonly List<FlowchartNode> _nodes;
        private readonly List<FlowchartEdge> _edges;
        private int _at;

        internal Scanner(
            string text,
            int line,
            Dictionary<string, int> seats,
            List<FlowchartNode> nodes,
            List<FlowchartEdge> edges
        )
        {
            _text = text;
            _line = line;
            _seats = seats;
            _nodes = nodes;
            _edges = edges;
        }

        internal string? Failure { get; private set; }

        internal bool Run()
        {
            string head = Node();
            if (Failure is not null)
            {
                return false;
            }

            string from = head;
            while (true)
            {
                _at = Space(_at);
                if (_at >= _text.Length)
                {
                    return true;
                }

                if (_text[_at] != '-')
                {
                    Failure = Unread(_text[_at..]);
                    return false;
                }

                FlowchartEdge? edge = Edge(from);
                if (edge is null)
                {
                    return false;
                }

                _edges.Add(edge);
                from = edge.To;
            }
        }

        private FlowchartEdge? Edge(string from)
        {
            string rest = _text[_at..];
            bool dashed;
            bool arrowhead;
            string? label = null;
            if (rest.StartsWith("-->", StringComparison.Ordinal))
            {
                dashed = false;
                arrowhead = true;
                _at += 3;
                label = BarLabel();
            }
            else if (rest.StartsWith("---", StringComparison.Ordinal))
            {
                dashed = false;
                arrowhead = false;
                _at += 3;
                label = BarLabel();
            }
            else if (rest.StartsWith("-.->", StringComparison.Ordinal))
            {
                dashed = true;
                arrowhead = true;
                _at += 4;
                label = BarLabel();
            }
            else if (rest.StartsWith("--", StringComparison.Ordinal))
            {
                int close = _text.IndexOf("-->", _at + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    Failure = Unread(_text[_at..]);
                    return null;
                }

                label = Clean(_text[(_at + 2)..close]);
                dashed = false;
                arrowhead = true;
                _at = close + 3;
            }
            else if (rest.StartsWith("-.", StringComparison.Ordinal))
            {
                int close = _text.IndexOf(".->", _at + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    Failure = "an unterminated dotted link needs its '.->'";
                    return null;
                }

                label = Clean(_text[(_at + 2)..close]);
                dashed = true;
                arrowhead = true;
                _at = close + 3;
            }
            else
            {
                Failure = Unread(_text[_at..]);
                return null;
            }

            string to = Node();
            if (Failure is not null)
            {
                return null;
            }

            return new FlowchartEdge(
                from,
                to,
                label is { Length: > 0 } text ? text : null,
                dashed,
                arrowhead
            );
        }

        private string BarLabel()
        {
            if (_at >= _text.Length || _text[_at] != '|')
            {
                return string.Empty;
            }

            int close = _text.IndexOf('|', _at + 1);
            if (close < 0)
            {
                Failure = "a bar label needs its closing '|'";
                return string.Empty;
            }

            string label = Clean(_text[(_at + 1)..close]);
            _at = close + 1;
            return label;
        }

        private string Node()
        {
            _at = Space(_at);
            int start = _at;
            while (
                _at < _text.Length
                && (char.IsLetterOrDigit(_text[_at]) || _text[_at] is '_' or '-' or '\'')
            )
            {
                _at++;
            }

            string id = _text[start.._at];
            if (id.Length == 0 || !char.IsLetterOrDigit(id[0]))
            {
                Failure = Unread(id.Length == 0 ? _text[_at..] : id);
                return string.Empty;
            }

            string? shape = Shape();
            if (Failure is not null)
            {
                return string.Empty;
            }

            IReadOnlyList<string> label = shape is null
                ? (IReadOnlyList<string>)[id]
                : MermaidLabels.Lines(shape);
            if (!_seats.TryGetValue(id, out int index))
            {
                index = _nodes.Count;
                _seats[id] = index;
                _nodes.Add(new FlowchartNode(id, label));
            }

            return id;
        }

        private string? Shape()
        {
            _at = Space(_at);
            if (_at >= _text.Length)
            {
                return null;
            }

            int open = _at;
            string closer;
            switch (_text[open])
            {
                case '[':
                    closer = "]";
                    break;
                case '{':
                    closer = "}";
                    break;
                case '(':
                    closer = _text[Math.Min(open + 1, _text.Length - 1)] == '[' ? "])" : ")";
                    break;
                default:
                    return null;
            }

            int close = _text.IndexOf(closer, open + closer.Length, StringComparison.Ordinal);
            if (close < 0)
            {
                Failure = $"line {_line}: a node label needs its closing '{closer}'";
                return null;
            }

            string raw = _text[(open + closer.Length)..close].Trim();
            _at = close + closer.Length;
            return Clean(raw);
        }

        private int Space(int at) => at < _text.Length && _text[at] == ' ' ? at + 1 : at;

        private static string Clean(string raw)
        {
            string text = raw.Trim();
            if (text.Length > 1 && text[0] == '"' && text[^1] == '"')
            {
                text = text[1..^1].Trim();
            }

            return text.Replace("\"", "");
        }

        private string Unread(string rest)
        {
            string clipped = rest.Length <= 24 ? rest : rest[..24] + "…";
            return $"line {_line}: I cannot read '{clipped}'; keep it to labels like [text] and edges like -->";
        }
    }
}

/// <summary>The label splitting shared by node labels and notes: <c>&lt;br/&gt;</c> breaks a line.</summary>
internal static class MermaidLabels
{
    internal static IReadOnlyList<string> Lines(string raw)
    {
        var lines = new List<string>();
        int cursor = 0;
        while (cursor < raw.Length)
        {
            int br = FindBr(raw, cursor);
            if (br < 0)
            {
                lines.Add(raw[cursor..]);
                return lines;
            }

            lines.Add(raw[cursor..br]);
            cursor = BrEnd(raw, br);
        }

        return lines;
    }

    private static int FindBr(string text, int from)
    {
        int index = from - 1;
        while ((index = text.IndexOf('<', index + 1)) >= 0)
        {
            int close = text.IndexOf('>', index + 1);
            if (close < 0 || close - index > 6)
            {
                continue;
            }

            string tag = text[index..(close + 1)];
            if (
                tag.Equals("<br>", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("<br/>", StringComparison.OrdinalIgnoreCase)
                || tag.Equals("<br />", StringComparison.OrdinalIgnoreCase)
            )
            {
                return index;
            }
        }

        return -1;
    }

    private static int BrEnd(string text, int br)
    {
        int close = text.IndexOf('>', br);
        return close < 0 ? text.Length : close + 1;
    }
}
