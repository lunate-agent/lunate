using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The quick/detailed mode spine (spec: Quick and detailed modes): Quick is the default and skips
/// the detailed-only steps, Detailed walks them, switching keeps every collected answer, the review
/// names every default Quick left, and a scoped entry stays detailed.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardModeTests
{
    [PosixFact]
    public void Quick_is_the_default_and_skips_the_detailed_only_steps()
    {
        var model = new SetupWizardModel();

        Assert.Equal(SetupWizardMode.Quick, model.Mode);
        Assert.True(model.IsQuick);
        Assert.Equal(
            [
                SetupStep.Provider,
                SetupStep.Credential,
                SetupStep.ConnectionTest,
                SetupStep.Model,
                SetupStep.ModelCheck,
                SetupStep.ChatProfile,
                SetupStep.Summary,
            ],
            Walked(model)
        );
    }

    [PosixFact]
    public void Detailed_walks_every_step_in_the_order()
    {
        var model = new SetupWizardModel();
        model.RecordMode(SetupWizardMode.Detailed);

        Assert.False(model.IsQuick);
        Assert.Equal(
            [
                SetupStep.Provider,
                SetupStep.Credential,
                SetupStep.ConnectionTest,
                SetupStep.Model,
                SetupStep.ModelCheck,
                SetupStep.ChatProfile,
                SetupStep.FastProfile,
                SetupStep.DecisionProfile,
                SetupStep.Decisions,
                SetupStep.Summary,
            ],
            Walked(model)
        );
    }

    [PosixFact]
    public void Switching_mode_keeps_every_collected_answer()
    {
        SetupWizardModel model = Answered();

        model.ToggleMode();

        Assert.Equal(SetupWizardMode.Detailed, model.Mode);
        Assert.NotNull(model.Provider);
        Assert.NotNull(model.Credential);
        Assert.NotNull(model.Model);
        Assert.NotNull(model.ChatProfile);

        model.ToggleMode();

        Assert.True(model.IsQuick);
        Assert.NotNull(model.Provider);
        Assert.NotNull(model.Credential);
        Assert.NotNull(model.Model);
        Assert.NotNull(model.ChatProfile);
    }

    [PosixFact]
    public void Quick_review_names_every_default_it_skipped_with_the_change_command()
    {
        SetupWizardModel model = Answered(custom: true);

        string review = string.Join("\n", SetupWizardSummary.Render(model));

        Assert.Contains("setup mode: quick", review, StringComparison.Ordinal);
        Assert.Contains("left at default / unchanged", review, StringComparison.Ordinal);
        foreach (
            string setting in new[]
            {
                "custom API family",
                "credential placement",
                "custom region",
                "custom models URL",
                "thinking level",
                "fast usage",
                "decision profile",
                "decisions.approvals",
                "decisions.routing",
                "routing descriptions",
                "decisions.questions",
                "decisions.tool",
            }
        )
        {
            Assert.Contains(
                review.Split('\n'),
                line =>
                    line.Contains(setting, StringComparison.Ordinal)
                    && (
                        line.Contains("/setup", StringComparison.Ordinal)
                        || line.Contains("/settings", StringComparison.Ordinal)
                    )
            );
        }

        Assert.Contains("· /setup", review, StringComparison.Ordinal);
        Assert.Contains("· /settings", review, StringComparison.Ordinal);
    }

    [PosixFact]
    public void A_detailed_review_has_no_left_at_default_block()
    {
        SetupWizardModel model = Answered();
        model.RecordMode(SetupWizardMode.Detailed);

        string review = string.Join("\n", SetupWizardSummary.Render(model));

        Assert.Contains("setup mode: detailed", review, StringComparison.Ordinal);
        // A detailed walk asks every setting, so the review names no Quick-only default even
        // though the unchanged block still lists the steps this walk skipped.
        Assert.DoesNotContain("custom API family", review, StringComparison.Ordinal);
        Assert.DoesNotContain("thinking level", review, StringComparison.Ordinal);
        Assert.Empty(model.LeftAtDefault);
    }

    [PosixFact]
    public void A_scoped_entry_stays_detailed_regardless_of_the_remembered_mode()
    {
        var model = new SetupWizardModel();
        model.Begin(WizardEntry.AddProfile);

        Assert.False(model.IsQuick);
        Assert.Empty(model.LeftAtDefault);
        Assert.Equal(SetupStep.FastProfile, model.Next(SetupStep.ChatProfile));
        Assert.Equal(SetupStep.DecisionProfile, model.LastWalked);
    }

    private static IReadOnlyList<SetupStep> Walked(SetupWizardModel model)
    {
        List<SetupStep> steps = [];
        SetupStep step = model.Step;
        while (step is not SetupStep.Summary)
        {
            steps.Add(step);
            step = model.Next(step);
        }

        steps.Add(SetupStep.Summary);
        return steps;
    }

    private static SetupWizardModel Answered(bool custom = false)
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                custom ? "local" : "openai",
                custom,
                custom ? "eu" : null,
                custom ? new Uri("https://local.example.test/v1") : null,
                "openai-chat",
                CredentialPlacement.Bearer,
                custom ? new Uri("https://local.example.test/v1/models") : null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:OPENAI_API_KEY"));
        model.RecordConnectionTest(new WizardCheckResult(WizardProgress.Passed));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat", "effort"));
        model.RecordModelCheck(new WizardCheckResult(WizardProgress.Passed));
        model.RecordChatProfile(new WizardProfileChoice("chat-main", "gpt-5", null));
        return model;
    }
}
