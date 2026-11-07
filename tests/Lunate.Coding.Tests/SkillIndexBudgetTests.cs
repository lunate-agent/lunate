using System.Globalization;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The index budget: descriptions trim to their first sentence, budgets derive from the model's
/// context window (soft 2%, hard 5%), a numeric `skills.indexBudget` replaces both, and
/// `AGENTS.md`-named skills survive the ceiling cut in full.
/// </summary>
public sealed class SkillIndexBudgetTests
{
    private static SkillDescriptor Skill(
        TempDirectory temp,
        string name,
        string description = "Does a thing",
        SkillScope scope = SkillScope.Project,
        string? extensionId = null
    )
    {
        string directory = Path.Combine(
            temp.File("skills-store"),
            extensionId ?? scope.ToString(),
            name
        );
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "SKILL.md");
        File.WriteAllText(file, $"---\nname: {name}\ndescription: {description}\n---\nBody\n");
        return new SkillDescriptor(
            name,
            description,
            "Body",
            directory,
            file,
            scope,
            extensionId,
            0,
            0,
            CommandOnly: false,
            Valid: true,
            InvalidReason: null
        );
    }

    /// <summary>A plain pack whose description has no sentence boundary, so every line runs to cap.</summary>
    private static List<SkillDescriptor> CappedPack(TempDirectory temp, int count) =>
        [
            .. Enumerable
                .Range(0, count)
                .Select(index => Skill(temp, $"skill-{index:D3}", new string('d', 1000))),
        ];

    [Fact]
    public void A_description_trims_to_its_first_sentence()
    {
        using var temp = new TempDirectory();

        SkillIndex.Composed index = SkillIndex.Compose(
            [Skill(temp, "greek", "Parses Gherkin. Also renders tables. Third sentence.")],
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 128_000),
            new HashSet<string>(StringComparer.Ordinal)
        );

        Assert.Contains("- greek: Parses Gherkin. [", index.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Also renders tables", index.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sentence_never_truncates_at_an_abbreviation()
    {
        using var temp = new TempDirectory();

        SkillIndex.Composed index = SkillIndex.Compose(
            [Skill(temp, "greek", "Loads files e.g. references. Second sentence.")],
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 128_000),
            new HashSet<string>(StringComparer.Ordinal)
        );

        // `e.g.` is followed by a lowercase letter, so the first sentence runs on.
        Assert.Contains(
            "- greek: Loads files e.g. references. [",
            index.Text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("Second sentence", index.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_description_without_a_sentence_boundary_is_still_capped_at_200()
    {
        using var temp = new TempDirectory();

        SkillIndex.Composed index = SkillIndex.Compose(
            [Skill(temp, "long", new string('x', 1000))],
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 128_000),
            new HashSet<string>(StringComparer.Ordinal)
        );

        int start = index.Text.IndexOf("long: ", StringComparison.Ordinal) + "long: ".Length;
        int end = index.Text.IndexOf(" [", start, StringComparison.Ordinal);
        string carried = index.Text[start..end];
        Assert.Equal(201, carried.Length);
        Assert.EndsWith("…", carried, StringComparison.Ordinal);
    }

    [Fact]
    public void The_budget_scales_with_the_models_context_window()
    {
        SkillIndex.SkillsBudget at128k = SkillIndex.SkillsBudget.FromSettings(
            new SkillsSettings(),
            128_000
        );
        Assert.Equal(2560, at128k.SoftTokens);
        Assert.Equal(6400, at128k.HardTokens);

        // A zero window degrades to the same default as the compaction hook.
        SkillIndex.SkillsBudget unknown = SkillIndex.SkillsBudget.FromSettings(
            new SkillsSettings(),
            0
        );
        Assert.Equal(at128k, unknown);
    }

    [Fact]
    public void A_pack_above_the_hard_ceiling_overflows_and_the_cut_stays_deterministic()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills = CappedPack(temp, 10);

        SkillIndex.Composed index = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 10_000),
            new HashSet<string>(StringComparer.Ordinal)
        );

        // The estimated tokens land under the 5% ceiling after the cut.
        Assert.True(
            index.EstimatedTokens <= 500,
            $"estimated {index.EstimatedTokens} tokens over a 500-token ceiling"
        );
        Assert.True(index.OverflowCount > 0, "no skill overflowed a 500-token ceiling");
        Assert.Contains("more skills (not indexed):", index.Text, StringComparison.Ordinal);

        SkillIndex.Composed again = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 10_000),
            new HashSet<string>(StringComparer.Ordinal)
        );
        Assert.Equal(index.Text, again.Text);
    }

    [Fact]
    public void A_complete_pack_over_the_soft_threshold_stays_whole_and_notices()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills = CappedPack(temp, 4);

        SkillIndex.Composed index = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 10_000),
            new HashSet<string>(StringComparer.Ordinal)
        );

        Assert.Equal(4, index.IndexedCount);
        Assert.Empty(index.NotIndexed);
        Assert.Equal(0, index.OverflowCount);
        Assert.True(
            index.EstimatedTokens > 200,
            $"estimated {index.EstimatedTokens} tokens under the 200-token soft threshold"
        );
        string? notice = SkillIndex.SoftNotice(index);
        Assert.NotNull(notice);
        Assert.Contains("fully indexed", notice, StringComparison.Ordinal);
        Assert.Null(SkillIndex.HardCeilingWarning(index));
    }

    [Fact]
    public void An_unlimited_budget_indexes_everything_in_silence()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills = CappedPack(temp, 10);
        SkillIndex.SkillsBudget unlimited = SkillIndex.SkillsBudget.FromSettings(
            new SkillsSettings { Unlimited = true },
            10_000
        );

        SkillIndex.Composed index = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            unlimited,
            new HashSet<string>(StringComparer.Ordinal)
        );

        Assert.Equal(10, index.IndexedCount);
        Assert.Empty(index.NotIndexed);
        Assert.Equal(0, index.OverflowCount);
        Assert.DoesNotContain("not indexed", index.Text, StringComparison.Ordinal);
        Assert.Null(SkillIndex.SoftNotice(index));
        Assert.Null(SkillIndex.HardCeilingWarning(index));
    }

    [Fact]
    public void A_numeric_budget_overflows_past_that_budget_alone()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills = CappedPack(temp, 10);
        SkillIndex.SkillsBudget budget = SkillIndex.SkillsBudget.FromSettings(
            new SkillsSettings { IndexBudget = 150 },
            200_000
        );

        SkillIndex.Composed index = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            budget,
            new HashSet<string>(StringComparer.Ordinal)
        );

        Assert.True(
            index.EstimatedTokens <= 150,
            $"estimated {index.EstimatedTokens} tokens over the 150-token budget"
        );
        Assert.True(index.OverflowCount > 0);
        Assert.NotNull(SkillIndex.HardCeilingWarning(index));
    }

    [Fact]
    public void AGENTS_md_named_skills_survive_a_ceiling_cut_and_the_overflow_warns()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills = CappedPack(temp, 10);
        SkillIndex.SkillsBudget budget = SkillIndex.SkillsBudget.FromSettings(
            new SkillsSettings(),
            10_000
        );
        HashSet<string> protectedNames = new(
            SkillIndex.NamesInInstructions(
                skills,
                "Run skill-000, then skill-005; never skip them."
            ),
            StringComparer.Ordinal
        );
        Assert.True(
            protectedNames.SetEquals((string[])["skill-000", "skill-005"]),
            $"matched: {string.Join(", ", protectedNames)}"
        );

        SkillIndex.Composed index = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            budget,
            protectedNames
        );

        // The protected skills keep their full lines whatever the cut dropped.
        Assert.Contains("- skill-000: ", index.Text, StringComparison.Ordinal);
        Assert.Contains("- skill-005: ", index.Text, StringComparison.Ordinal);
        Assert.True(
            index.OverflowCount > 0,
            "nothing overflowed although the pack exceeds the ceiling"
        );
        Assert.Equal(index.NotIndexed.Count, index.OverflowCount);
        string? warning = SkillIndex.HardCeilingWarning(index);
        Assert.NotNull(warning);
        Assert.Contains(
            index.OverflowCount.ToString(CultureInfo.InvariantCulture),
            warning,
            StringComparison.Ordinal
        );
        Assert.Contains("without their description", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void When_every_skill_is_agents_md_named_the_ceiling_yields()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills = CappedPack(temp, 10);
        HashSet<string> protectedNames = new(
            SkillIndex.NamesInInstructions(
                skills,
                "skill-000 skill-001 skill-002 skill-003 skill-004 skill-005 skill-006 skill-007 skill-008 skill-009"
            ),
            StringComparer.Ordinal
        );

        SkillIndex.Composed index = SkillIndex.Compose(
            skills,
            temp.Root,
            2000,
            SkillIndex.SkillsBudget.FromSettings(new SkillsSettings(), 10_000),
            protectedNames
        );

        Assert.Equal(10, index.IndexedCount);
        Assert.Equal(0, index.OverflowCount);
        Assert.Empty(index.NotIndexed);
        Assert.DoesNotContain("not indexed", index.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Names_match_only_as_whole_names()
    {
        using var temp = new TempDirectory();
        List<SkillDescriptor> skills =
        [
            Skill(temp, "alpha"),
            Skill(temp, "beta"),
            Skill(temp, "gamma"),
        ];

        HashSet<string> matched = new(
            SkillIndex.NamesInInstructions(
                skills,
                "alpha-locked, alphabetical order, then gamma (use beta here too)."
            ),
            StringComparer.Ordinal
        );

        Assert.True(
            matched.SetEquals((string[])["gamma", "beta"]),
            $"matched: {string.Join(", ", matched)}"
        );
    }
}
