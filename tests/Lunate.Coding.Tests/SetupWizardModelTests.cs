using Lunate.Ai;

namespace Lunate.Coding.Tests;

[CollectionDefinition("setup-wizard", DisableParallelization = true)]
public sealed class SetupWizardCollection
{
    public const string Name = "setup-wizard";
}

/// <summary>
/// The wizard's staged-state rules (spec: The full walk; Nothing written before the summary
/// confirms): answers stage in memory, a step failed by a check goes back without losing later
/// answers, skipping a step invents nothing, and the summary knows whether anything would change.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardModelTests
{
    [PosixFact]
    public void Fresh_walk_starts_at_the_provider_step_with_no_answers()
    {
        var model = new SetupWizardModel();

        Assert.Equal(SetupStep.Provider, model.Step);
        Assert.Null(model.Provider);
        Assert.Null(model.Credential);
        Assert.Null(model.ConnectionTest);
        Assert.Null(model.Model);
        Assert.Null(model.ModelCheck);
        Assert.Null(model.ChatProfile);
        Assert.False(model.FastProfile);
        Assert.Null(model.DecisionProfile);
        Assert.Null(model.Decisions);
        Assert.Empty(model.SkippedSteps);
        Assert.False(model.HasStagedWrites);
    }

    [PosixFact]
    public void Go_back_to_the_credential_step_clears_only_the_answers_behind_it()
    {
        SetupWizardModel model = FullyAnswered();

        model.Enter(SetupStep.Credential);
        model.RecordCredential(new WizardCredentialChoice("env:OPENAI_API_KEY"));

        Assert.NotNull(model.Provider);
        Assert.Null(model.ConnectionTest);
        Assert.Null(model.Model);
        Assert.Null(model.ModelCheck);
        // The steps that depended on the credential re-walk; steps independent of it keep their answers.
        Assert.Null(model.ChatProfile);
        Assert.NotNull(model.DecisionProfile);
    }

    [PosixFact]
    public void Go_back_to_the_model_step_clears_the_model_check_only()
    {
        SetupWizardModel model = FullyAnswered();

        model.Enter(SetupStep.Model);
        model.RecordModel(new WizardModelChoice("openai/gpt-5", "openai-chat"));

        Assert.NotNull(model.Provider);
        Assert.NotNull(model.Credential);
        Assert.NotNull(model.ConnectionTest);
        Assert.Null(model.ModelCheck);
        // The thinking level is chosen from the model's declared styles, so it re-walks with the model.
        Assert.Null(model.ChatProfile);
        Assert.NotNull(model.DecisionProfile);
    }

    [PosixFact]
    public void Skip_marks_the_step_and_the_summary_knows_it()
    {
        var model = new SetupWizardModel();

        model.Enter(SetupStep.ConnectionTest);
        model.Skip(SetupStep.ConnectionTest);
        model.Enter(SetupStep.ModelCheck);
        model.Skip(SetupStep.ModelCheck);

        Assert.Equal([SetupStep.ConnectionTest, SetupStep.ModelCheck], model.SkippedSteps);
    }

    [PosixFact]
    public void Skip_everything_writes_nothing_but_the_walk_completes()
    {
        var model = new SetupWizardModel();
        foreach (SetupStep step in SetupWizardModel.Order)
        {
            model.Enter(step);
            model.Skip(step);
        }

        Assert.False(model.HasStagedWrites);
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
            ],
            model.SkippedSteps
        );
    }

    [PosixFact]
    public void Any_staged_answer_marks_the_summary_as_a_write()
    {
        var model = new SetupWizardModel();
        model.Enter(SetupStep.ConnectionTest);
        model.Skip(SetupStep.ConnectionTest);
        model.Skip(SetupStep.ModelCheck);

        model.Enter(SetupStep.Model);
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));

        Assert.True(model.HasStagedWrites);
    }

    [PosixFact]
    public void A_skipped_decision_profile_and_decisions_read_as_skipped()
    {
        var model = new SetupWizardModel();

        model.RecordDecisionProfile(null);
        model.RecordDecisions(null);

        Assert.Equal([SetupStep.DecisionProfile, SetupStep.Decisions], model.SkippedSteps);
        Assert.Null(model.DecisionProfile);
        Assert.Null(model.Decisions);
    }

    [PosixFact]
    public void The_path_groups_the_ten_steps_into_four_stages()
    {
        Assert.Equal(SetupStage.Provider, SetupWizardModel.StageOf(SetupStep.Provider));
        Assert.Equal(SetupStage.Provider, SetupWizardModel.StageOf(SetupStep.ConnectionTest));
        Assert.Equal(SetupStage.Model, SetupWizardModel.StageOf(SetupStep.Model));
        Assert.Equal(SetupStage.Model, SetupWizardModel.StageOf(SetupStep.ModelCheck));
        Assert.Equal(SetupStage.Profile, SetupWizardModel.StageOf(SetupStep.ChatProfile));
        Assert.Equal(SetupStage.Profile, SetupWizardModel.StageOf(SetupStep.Decisions));
        Assert.Equal(SetupStage.Review, SetupWizardModel.StageOf(SetupStep.Summary));

        var model = new SetupWizardModel();
        model.Enter(SetupStep.ConnectionTest);
        Assert.Equal(1, model.StagePosition);
        Assert.Contains("Provider › Model › Profile › Review", model.HeaderLine, StringComparison.Ordinal);
        Assert.Contains("1/4", model.HeaderLine, StringComparison.Ordinal);

        model.Enter(SetupStep.Model);
        Assert.Equal(2, model.StagePosition);
        Assert.Contains("2/4", model.HeaderLine, StringComparison.Ordinal);

        model.Enter(SetupStep.Decisions);
        Assert.Equal(3, model.StagePosition);

        model.Enter(SetupStep.Summary);
        Assert.Equal(4, model.StagePosition);
    }

    [PosixFact]
    public void An_answer_is_a_staged_value_or_an_explicit_skip()
    {
        var model = new SetupWizardModel();

        Assert.False(model.HasAnswer(SetupStep.Provider));
        model.Enter(SetupStep.Provider);
        model.Skip(SetupStep.Provider);
        Assert.True(model.HasAnswer(SetupStep.Provider));

        Assert.False(model.HasAnswer(SetupStep.ConnectionTest));
        model.RecordConnectionTest(new WizardCheckResult(WizardProgress.Skipped));
        Assert.True(model.HasAnswer(SetupStep.ConnectionTest));

        Assert.False(model.HasAnswer(SetupStep.Model));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        Assert.True(model.HasAnswer(SetupStep.Model));
        Assert.False(model.HasAnswer(SetupStep.Summary));
    }

    [PosixFact]
    public void Forward_stops_at_the_first_unanswered_step()
    {
        SetupWizardModel model = FullyAnswered();

        // The decisions answer is the only one the fully-answered fixture leaves out.
        Assert.False(model.HasAnswer(SetupStep.Decisions));
        Assert.Equal(SetupStep.Decisions, model.FirstUnansweredAfter(SetupStep.Provider));
        Assert.Contains(
            SetupStep.ChatProfile,
            model.ReplayedSteps(SetupStep.Provider, SetupStep.Decisions)
        );
        Assert.DoesNotContain(
            SetupStep.Provider,
            model.ReplayedSteps(SetupStep.Provider, SetupStep.Decisions)
        );

        model.RecordDecisions(
            new WizardDecisionsChoice(null, null, null, [], new Dictionary<string, string>())
        );
        Assert.Equal(SetupStep.Summary, model.FirstUnansweredAfter(SetupStep.Provider));
    }

    [PosixFact]
    public void A_change_clears_the_answers_forward_may_not_skip_into()
    {
        SetupWizardModel model = FullyAnswered();

        model.Enter(SetupStep.Provider);
        model.RecordProvider(
            new WizardProviderChoice(
                "anthropic",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "anthropic-messages",
                Placement: CredentialPlacement.XApiKey,
                ModelsUrl: null
            )
        );

        // The credential and everything behind it were cleared, so Forward stops there instead of
        // replaying an answer the change invalidated.
        Assert.False(model.HasAnswer(SetupStep.Credential));
        Assert.Equal(SetupStep.Credential, model.FirstUnansweredAfter(SetupStep.Provider));
    }

    [PosixFact]
    public void End_of_walk_navigation_is_clamped_to_the_entrys_range()
    {
        var model = new SetupWizardModel();
        model.Enter(SetupStep.Provider);
        Assert.Equal(SetupStep.Provider, model.PreviousStep());
        model.Enter(SetupStep.Decisions);
        Assert.Equal(SetupStep.Summary, model.NextStep());

        var entry = new SetupWizardModel();
        entry.Begin(WizardEntry.AddProfile);
        Assert.Equal(SetupStep.Model, entry.Step);
        Assert.Equal(SetupStep.Model, entry.PreviousStep());

        var provider = new SetupWizardModel();
        provider.Begin(WizardEntry.AddProvider);
        provider.Enter(SetupStep.ConnectionTest);
        Assert.Equal(SetupStep.Summary, provider.NextStep());
    }

    private static SetupWizardModel FullyAnswered()
    {
        var model = new SetupWizardModel();
        model.Enter(SetupStep.Provider);
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: "global",
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.Enter(SetupStep.Credential);
        model.RecordCredential(new WizardCredentialChoice("auth:openai", Key: "typed-key"));
        model.RecordConnectionTest(new WizardCheckResult(WizardProgress.Passed));
        model.RecordModel(new WizardModelChoice("openai/gpt-5", "openai-chat"));
        model.RecordModelCheck(new WizardCheckResult(WizardProgress.Passed));
        model.RecordChatProfile(new WizardProfileChoice("chat-main", "openai/gpt-5", "high"));
        model.RecordFastProfile(false);
        model.RecordDecisionProfile(
            new WizardUsageChoice(
                "decider",
                "openai",
                "jev-1.14",
                ProviderApiFamilies.SystemOne,
                Native: true,
                PinnedVersion: "jev-1.14"
            )
        );
        return model;
    }
}
