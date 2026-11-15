using System.Text;
using Mermaid.Terminal;

// The VHS tapes drive this program: it renders one of the documented examples through
// Mermaid.Terminal and paints the frame runs the way the TUI does, so the captured image is
// the library's output rather than a copy of a committed text fixture.
Console.OutputEncoding = Encoding.UTF8;

bool ascii = args.Contains("--ascii");
string name = args.Where(static argument => argument != "--ascii").ToArray() switch
{
    [var only] => only,
    _ => "",
};
(string Source, int Width)? example = name switch
{
    "flowchart-td" => (
        """
        flowchart TD
            Coding[The executable] --> Agent[Agent loop]
            Coding --> Tui[TUI]
            Agent --> Ai[Lunate.Ai]
            Prov(["Provider APIs"]) --> Agent
            Coding -."requests".-> Prov
        """,
        80
    ),
    "flowchart-lr" => (
        """
        flowchart LR
            Loop["The harness<br/>the loop"] --> Rec["recorder"] -.->|"new events"| Acc[accumulator]
            Loop --> Log[logging]
        """,
        100
    ),
    "sequence" => (
        """
        sequenceDiagram
            participant C as Consumer
            participant H as AgentHarness

            C->>H: RunAsync(userInput)
            H-->>C: TextMessageStart / Content / End
            Note over H: spans per run
        """,
        80
    ),
    "fallback" => (
        """
        stateDiagram-v2
            [*] --> q0
            q0 --> q1: input
        """,
        80
    ),
    _ => null,
};
if (example is not { } chosen)
{
    Console.Error.WriteLine(
        "usage: MermaidTape [--ascii] <flowchart-td|flowchart-lr|sequence|fallback>"
    );
    return 2;
}

MermaidRenderResult result = MermaidRenderer.Render(
    chosen.Source,
    chosen.Width,
    ascii ? MermaidGlyphSet.Ascii : MermaidGlyphSet.Unicode
);
if (result is MermaidFallbackResult fallback)
{
    foreach (string line in fallback.Source)
    {
        Console.WriteLine(line);
    }

    Console.WriteLine($"mermaid: {fallback.Reason} — showing the source");
    return 0;
}

foreach (MermaidLine line in ((MermaidDiagramResult)result).Lines)
{
    WriteStyled(line);
}

return 0;

static void WriteStyled(MermaidLine line)
{
    foreach (MermaidSpan span in line.Spans)
    {
        Console.Write(span.Kind == MermaidSpanKind.Frame ? "\u001b[2m" : "\u001b[22m");
        Console.Write(span.Text);
    }

    Console.Write("\u001b[0m");
    Console.WriteLine();
}
