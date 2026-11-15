namespace Mermaid.Terminal.Tests;

/// <summary>
/// The sequence half of task 4: the grammar the parser accepts, the drawing the renderer
/// produces from it, and the fallbacks the diagram gate gives when the sequence is not one the
/// renderer can draw.
/// </summary>
public sealed class MermaidSequenceTests
{
    [Fact]
    public void The_parser_keeps_participants_and_speech_in_order()
    {
        string[] source =
        [
            "sequenceDiagram",
            "participant C as Consumer",
            "participant H as AgentHarness",
            "C->>H: RunAsync",
            "H-->C: Started",
            "C->H: plain",
            "H-->>C: done",
            "Note over C, H: spans",
        ];

        Assert.True(
            SequenceParser.TryParse(source, out SequenceModel? model, out string? failure),
            failure
        );
        Assert.Null(failure);
        Assert.NotNull(model);

        Assert.Collection(
            model!.Participants,
            participant =>
            {
                Assert.Equal("C", participant.Id);
                Assert.Equal("Consumer", participant.Name);
            },
            participant =>
            {
                Assert.Equal("H", participant.Id);
                Assert.Equal("AgentHarness", participant.Name);
            }
        );

        var messages = model.Statements.OfType<SequenceMessage>().ToList();
        Assert.Collection(
            messages,
            message => Assert.Equal((0, 1, "RunAsync", false, true), Details(message)),
            message => Assert.Equal((1, 0, "Started", true, false), Details(message)),
            message => Assert.Equal((0, 1, "plain", false, false), Details(message)),
            message => Assert.Equal((1, 0, "done", true, true), Details(message))
        );

        var note = Assert.IsType<SequenceNote>(model.Statements[^1]);
        Assert.Equal("spans", note.Content);
        Assert.Equal([0, 1], note.Over);
    }

    [Fact]
    public void A_message_implies_a_participant_that_was_not_declared()
    {
        string[] source = ["sequenceDiagram", "A->>B: hi"];

        Assert.True(
            SequenceParser.TryParse(source, out SequenceModel? model, out string? failure),
            failure
        );

        Assert.Collection(
            model!.Participants,
            participant => Assert.Equal(("A", "A"), (participant.Id, participant.Name)),
            participant => Assert.Equal(("B", "B"), (participant.Id, participant.Name))
        );
    }

    [Fact]
    public void An_unsupported_sequence_statement_names_its_line()
    {
        string[] source = ["sequenceDiagram", "participant A as Alice", "loop every minute"];

        Assert.False(SequenceParser.TryParse(source, out _, out string? failure));
        Assert.Equal("line 3: unsupported statement 'loop'", failure);
    }

    [Fact]
    public void A_sequence_draws_participant_boxes_lifelines_messages_and_notes()
    {
        string[] source =
        [
            "sequenceDiagram",
            "participant A as Alice",
            "participant B as Bob",
            "A->>B: hello",
            "Note over B: noted",
        ];

        MermaidDiagramResult result = Diagram(source);
        IReadOnlyList<string> lines = result.Lines.Select(static line => line.Text).ToList();
        Assert.Contains("Alice", lines[1]);
        Assert.Contains("Bob", lines[1]);
        Assert.Contains(lines, line => line.Contains('│'));
        Assert.Contains(lines, line => line.Contains('►'));
        Assert.Contains(lines, line => line.Contains("hello"));
        Assert.Contains(lines, line => line.Contains("noted"));
    }

    [Fact]
    public void A_message_label_that_does_not_fit_is_truncated_with_an_ellipsis()
    {
        string[] source =
        [
            "sequenceDiagram",
            "participant A as A",
            "participant B as B",
            "A->>B: a label far too long for the gap",
        ];

        string drawing = string.Join('\n', Diagram(source).Lines.Select(static line => line.Text));
        Assert.Contains('…', drawing);
        Assert.DoesNotContain("a label far too long for the gap", drawing);
    }

    [Fact]
    public void Ascii_sequence_keeps_the_geometry_and_swaps_the_glyphs()
    {
        string[] source =
        [
            "sequenceDiagram",
            "participant A as Alice",
            "participant B as Bob",
            "A->>B: hello",
            "B-->>A: done",
            "Note over B: noted",
        ];

        IReadOnlyList<string> unicode = Diagram(source, MermaidGlyphSet.Unicode)
            .Lines.Select(static line => line.Text)
            .ToList();
        IReadOnlyList<string> ascii = Diagram(source, MermaidGlyphSet.Ascii)
            .Lines.Select(static line => line.Text)
            .ToList();

        Assert.Equal(unicode.Count, ascii.Count);
        for (var index = 0; index < unicode.Count; index++)
        {
            Assert.Equal(unicode[index].Length, ascii[index].Length);
        }

        string drawing = string.Join('\n', ascii);
        Assert.Contains('+', drawing);
        Assert.Contains('-', drawing);
        Assert.Contains('|', drawing);
        Assert.Contains('>', drawing);
        Assert.Contains('<', drawing);
        Assert.DoesNotContain('┌', drawing);
    }

    [Fact]
    public void A_sequence_wider_than_the_terminal_falls_back_to_its_source()
    {
        string[] source =
        [
            "sequenceDiagram",
            "participant A as Alice The First Participant With A Long Name",
            "participant B as Bob",
            "A->>B: hello",
        ];

        MermaidFallbackResult fallback = Assert.IsType<MermaidFallbackResult>(
            MermaidRenderer.Render(string.Join('\n', source), 40)
        );

        Assert.Contains("columns wide, this terminal has 40", fallback.Reason);
        Assert.Equal(source, fallback.Source);
    }

    private static (int From, int To, string Content, bool Dotted, bool Head) Details(
        SequenceMessage message
    ) => (message.From, message.To, message.Content, message.Dotted, message.Head);

    private static MermaidDiagramResult Diagram(
        IEnumerable<string> source,
        MermaidGlyphSet glyphs = MermaidGlyphSet.Unicode
    ) =>
        Assert.IsType<MermaidDiagramResult>(
            MermaidRenderer.Render(string.Join('\n', source), int.MaxValue, glyphs)
        );
}
