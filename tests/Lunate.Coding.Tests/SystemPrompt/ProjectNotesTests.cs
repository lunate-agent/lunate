namespace Lunate.Coding.Tests;

/// <summary>
/// The project notes file: discovered at the repository root, placed after the AGENTS.md chain so
/// its facts never outrank a rule, and bounded on the read side.
/// </summary>
public sealed class ProjectNotesTests
{
    [Fact]
    public void Missing_file_produces_no_notes_block()
    {
        using var temp = new TempDirectory();

        string notes = ProjectNotes.Compose(new Workspace(temp.Root));

        Assert.Equal(string.Empty, notes);
    }

    [Fact]
    public void A_nested_launch_reads_the_repository_root_file()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.File(".git"));
        string nested = temp.File("src/App");
        Directory.CreateDirectory(nested);
        File.WriteAllText(temp.File(ProjectNotes.FileName), "builds with .NET 11");

        string notes = ProjectNotes.Compose(new Workspace(nested));

        Assert.Contains("### Project notes (LUNATE.md)", notes, StringComparison.Ordinal);
        Assert.Contains("builds with .NET 11", notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Agents_rules_precede_the_notes()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("AGENTS.md"), "a rule");
        File.WriteAllText(temp.File(ProjectNotes.FileName), "a fact");
        var workspace = new Workspace(temp.Root);

        string prompt = SystemPrompt.Compose(
            workspace,
            ["read"],
            new ShellResolver("/bin/sh"),
            new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.Zero)
        );

        int rule = prompt.IndexOf("a rule", StringComparison.Ordinal);
        int fact = prompt.IndexOf("a fact", StringComparison.Ordinal);
        Assert.True(rule >= 0 && fact > rule);
        Assert.Contains("the AGENTS.md rules", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void An_over_budget_file_shows_the_first_budget_and_names_the_omitted_count()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File(ProjectNotes.FileName),
            new string('a', ProjectNotes.MaxCharacters + 25)
        );

        string notes = ProjectNotes.Compose(new Workspace(temp.Root));

        Assert.Contains(
            new string('a', ProjectNotes.MaxCharacters),
            notes,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            new string('a', ProjectNotes.MaxCharacters + 1),
            notes,
            StringComparison.Ordinal
        );
        Assert.Contains("25 characters omitted", notes, StringComparison.Ordinal);
        Assert.Contains(ProjectNotes.FileName, notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Braces_in_notes_are_not_treated_as_prompt_tokens()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File(ProjectNotes.FileName), "Use {braces} and {} literally.");

        string prompt = SystemPrompt.Compose(
            new Workspace(temp.Root),
            ["read"],
            new ShellResolver("/bin/sh")
        );

        Assert.Contains("Use {braces} and {} literally.", prompt, StringComparison.Ordinal);
    }
}
