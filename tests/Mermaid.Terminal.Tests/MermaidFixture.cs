namespace Mermaid.Terminal.Tests;

/// <summary>
/// One mermaid-ascii golden file: the source before its <c>---</c> separator and the original
/// tool's own committed output after it. Keeping the upstream file shape makes the comparison
/// auditable: the expected text is the renderer's testdata, not something this repository wrote.
/// </summary>
internal sealed record MermaidFixture(string Source, string Expected)
{
    private const string Separator = "\n---\n";

    internal static MermaidFixture Load(string name)
    {
        string path = Path.Combine(FixturesDirectory, name);
        string text = File.ReadAllText(path).ReplaceLineEndings("\n");
        int at = text.IndexOf(Separator, StringComparison.Ordinal);
        if (at < 0)
        {
            throw new InvalidOperationException($"{path} carries no '---' separator");
        }

        string expected = text[(at + Separator.Length)..];
        if (expected.EndsWith('\n'))
        {
            expected = expected[..^1];
        }

        return new MermaidFixture(text[..at], expected);
    }

    internal static string FixturesDirectory =>
        Path.Combine(RepositoryRoot(), "tests", "Mermaid.Terminal.Tests", "Fixtures");

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "lunate.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "could not find the repository root from the test binary"
        );
    }
}
