using Spectre.Console;

namespace Lunate.Tui.Tests;

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
            MermaidSequenceParser.TryParse(
                source,
                out MermaidSequenceModel? model,
                out var failure
            ),
            failure?.Reason
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

        var messages = model.Speech.OfType<MermaidMessage>().ToList();
        Assert.Collection(
            messages,
            message => Assert.Equal((0, 1, "RunAsync", false), Details(message)),
            message => Assert.Equal((1, 0, "Started", true), Details(message)),
            message => Assert.Equal((0, 1, "plain", false), Details(message)),
            message => Assert.Equal((1, 0, "done", true), Details(message))
        );

        var note = Assert.IsType<MermaidNote>(model.Speech[^1]);
        Assert.Equal("spans", note.Content);
        Assert.Equal([0, 1], note.Over);
    }

    [Fact]
    public void A_message_implies_a_participant_that_was_not_declared()
    {
        string[] source = ["sequenceDiagram", "A->>B: hi"];

        Assert.True(
            MermaidSequenceParser.TryParse(
                source,
                out MermaidSequenceModel? model,
                out var failure
            ),
            failure?.Reason
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

        Assert.False(MermaidSequenceParser.TryParse(source, out _, out MermaidFailure? failure));
        Assert.Equal("line 3: unsupported statement 'loop'", failure!.Reason);
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

        MermaidResult result = MermaidSequence.TryDraw(source, unicode: true);
        Assert.NotNull(result.Canvas);

        IReadOnlyList<string> lines = TextLines(result.Canvas!);
        Assert.Contains("Alice", lines[1]);
        Assert.Contains("Bob", lines[1]);
        Assert.Contains(lines, line => line.Contains('│'));
        Assert.Contains(lines, line => line.Contains('▸'));
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

        MermaidResult result = MermaidSequence.TryDraw(source, unicode: true);
        Assert.NotNull(result.Canvas);

        string drawing = string.Join('\n', TextLines(result.Canvas!));
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
            "B-->A: done",
            "Note over B: noted",
        ];

        IReadOnlyList<string> unicode = TextLines(
            MermaidSequence.TryDraw(source, unicode: true).Canvas!
        );
        IReadOnlyList<string> ascii = TextLines(
            MermaidSequence.TryDraw(source, unicode: false).Canvas!
        );

        Assert.Equal(unicode.Count, ascii.Count);
        for (var index = 0; index < unicode.Count; index++)
        {
            Assert.Equal(unicode[index].Length, ascii[index].Length);
        }

        string drawing = string.Join('\n', ascii);
        Assert.Contains("+", drawing);
        Assert.Contains("-", drawing);
        Assert.Contains("|", drawing);
        Assert.Contains(">", drawing);
        Assert.Contains("<", drawing);
        Assert.DoesNotContain("╭", drawing);
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

        IReadOnlyList<StyledLine> rendered = MermaidDiagram.RenderFence(
            source,
            new TerminalCapabilities(ColorSystemSupport.NoColors, Unicode: true),
            terminalWidth: 40
        );

        string output = string.Join('\n', TextLines(rendered));
        Assert.Contains("mermaid: diagram is ", output);
        Assert.Contains("showing the source", output);
        Assert.Contains("participant A as", output);
        Assert.DoesNotContain("╭", output);
    }

    private static (int From, int To, string Content, bool Dashed) Details(
        MermaidMessage message
    ) => (message.From, message.To, message.Content, message.Dashed);

    private static IReadOnlyList<string> TextLines(MermaidCanvas canvas) =>
        TextLines(canvas.ToLines());

    private static IReadOnlyList<string> TextLines(IReadOnlyList<StyledLine> lines) =>
        lines.Select(line => string.Concat(line.Spans.Select(span => span.Text))).ToList();
}
