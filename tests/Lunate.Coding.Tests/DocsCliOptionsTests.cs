using System.Text.RegularExpressions;
using Lunate.TestSupport;
using Xunit;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>docs/cli.md</c> is the option reference a reader trusts instead of running <c>--help</c>, so
/// the two must agree in both directions: every option in the page's help-checked region has to be
/// one the binary accepts, and every option <c>--help</c> names has to appear on the page. Drift in
/// either direction is a reader who types something that does not exist, or cannot find something
/// that does.
/// </summary>
/// <remarks>
/// The checked region is delimited by <c>&lt;!-- cli-help: start --&gt;</c> and
/// <c>&lt;!-- cli-help: end --&gt;</c> because a few flags belong to a subcommand the top-level help
/// only sketches — <c>auth login --custom</c>'s endpoint answers live on the page that owns that
/// flow. Everything inside the region is required to be in the help output, so a flag cannot be
/// documented there before the binary accepts it and names it.
/// </remarks>
public sealed class DocsCliOptionsTests
{
    private const string Page = "docs/cli.md";
    private const string CheckedStart = "<!-- cli-help: start -->";
    private const string CheckedEnd = "<!-- cli-help: end -->";

    /// <summary>
    /// A flag token: two hyphens and a word, or one hyphen and a single letter. The lookbehind
    /// keeps hyphenated words (<c>read-only</c>) and the HTML comment delimiters out of the set.
    /// </summary>
    private static readonly Regex Flag = new(
        @"(?<![\w=/-])(--?[a-z][a-z0-9-]*)",
        RegexOptions.Compiled
    );

    private static readonly Regex ValueSuffix = new(@"=\S*$", RegexOptions.Compiled);

    [Fact]
    public void Every_option_in_the_checked_region_is_named_by_the_help_output()
    {
        HashSet<string> help = HelpFlags();
        List<string> undocumented = [];
        foreach (string option in CheckedRegionFlags())
        {
            if (!help.Contains(option))
            {
                undocumented.Add(option);
            }
        }

        Assert.True(
            undocumented.Count == 0,
            "docs/cli.md documents option(s) 'lunate --help' does not name: "
                + string.Join(", ", undocumented)
                + ". Either add them to CliHelp or move them outside the cli-help region."
        );
    }

    [Fact]
    public void Every_option_the_help_output_names_appears_on_the_page()
    {
        HashSet<string> page = PageFlags();
        List<string> undocumented = [];
        foreach (string option in HelpFlags())
        {
            if (!page.Contains(option))
            {
                undocumented.Add(option);
            }
        }

        Assert.True(
            undocumented.Count == 0,
            "'lunate --help' names option(s) docs/cli.md does not document: "
                + string.Join(", ", undocumented)
                + ". Add them to the page's option tables."
        );
    }

    [Fact]
    public void The_page_carries_a_checked_region()
    {
        string text = ReadPage();

        Assert.Contains(CheckedStart, text, StringComparison.Ordinal);
        Assert.Contains(CheckedEnd, text, StringComparison.Ordinal);
        Assert.NotEmpty(CheckedRegionFlags());
    }

    private static HashSet<string> CheckedRegionFlags()
    {
        string text = ReadPage();
        int start = text.IndexOf(CheckedStart, StringComparison.Ordinal);
        int end = text.IndexOf(CheckedEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"{Page} has no complete cli-help region");
        return FlagsIn(text[start..end]);
    }

    private static HashSet<string> PageFlags() => FlagsIn(ReadPage());

    /// <summary>
    /// The options the real parser advertises. The help is the binary's own text, captured through
    /// the same entry point a user's run uses, so no copy of the option list lives in this file.
    /// </summary>
    private static HashSet<string> HelpFlags()
    {
        using var writer = new StringWriter();
        int exitCode = Cli.Run(["--help"], writer, TextWriter.Null, CancellationToken.None);
        Assert.Equal(0, exitCode);
        return FlagsIn(writer.ToString());
    }

    private static HashSet<string> FlagsIn(string text)
    {
        HashSet<string> found = new(StringComparer.Ordinal);
        foreach (Match match in Flag.Matches(text))
        {
            // `--flag=value` documents its own form; the flag is everything before the '='.
            found.Add(ValueSuffix.Replace(match.Value, string.Empty));
        }

        return found;
    }

    private static string ReadPage() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Find(), "docs", "cli.md"));
}
