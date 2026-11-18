using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The skills notices and warnings in print mode: every skills line is a diagnostic and lands on
/// stderr, while stdout stays the pure answer a script redirects. The generic print-mode purity
/// assertions live in <see cref="ProcessSessionTests"/>; this class pins the skills-specific lines —
/// the index notice, the hard-ceiling warning and the over-limit description truncation warning.
/// </summary>
public sealed class SkillsPrintModeNoticeTests
{
    private const string TruncationWarning =
        "skills: verbose description exceeds the 1024-char spec limit; truncated in the index";

    private static void WriteSkill(
        string directory,
        string name,
        string description = "Does a thing",
        string body = "Body."
    )
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "SKILL.md"),
            $"---\nname: {name}\ndescription: {description}\n---\n\n{body}\n"
        );
    }

    [Fact]
    public async Task The_skills_notice_and_the_truncation_warning_go_to_stderr_only()
    {
        using var outer = new TempDirectory();
        string worktree = Path.Combine(outer.Root, "worktree");
        Directory.CreateDirectory(Path.Combine(worktree, ".git"));
        // Six 120-char descriptions measure above the 20,000-token window's 2% soft threshold,
        // so the index notice fires; one skill sits over the 1,024-character description limit,
        // so the truncation warning fires too — both in one print run.
        for (var index = 0; index < 6; index++)
        {
            WriteSkill(
                Path.Combine(worktree, ".agents", "skills", $"skill-{index:D2}"),
                $"skill-{index:D2}",
                description: new string('d', 120)
            );
        }

        WriteSkill(
            Path.Combine(worktree, ".agents", "skills", "verbose"),
            "verbose",
            description: new string('v', SkillIndex.MaxDescriptionLength + 400)
        );

        (ScriptedChatClient client, int exitCode, string output, string errors) =
            await SkillsPrintModeTests.RunTrustedAsync(outer, worktree, "small-model", 20_000);

        Assert.Equal(0, exitCode);
        // Both skills lines are diagnostics, one `lunate: ` line each, on stderr.
        Assert.Contains($"lunate: {TruncationWarning}\n", errors, StringComparison.Ordinal);
        Assert.Contains(
            "lunate: skills: the skills index measures",
            errors,
            StringComparison.Ordinal
        );
        // stdout carries the final answer only: no notice, no warning, no skills line.
        Assert.Equal("ok", output);
        Assert.DoesNotContain("lunate:", output, StringComparison.Ordinal);
        Assert.DoesNotContain("skills", output, StringComparison.Ordinal);
        // The over-limit description still reaches the index, sliced at the 1,024-char limit.
        string system = Assert
            .Single(client.Requests[0], message => message.Role == ChatRole.System)
            .Text;
        Assert.Contains("- verbose: ", system, StringComparison.Ordinal);
        Assert.Contains(
            new string('v', SkillIndex.MaxDescriptionLength) + "…",
            system,
            StringComparison.Ordinal
        );
    }
}
