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
            new WizardDecisionChoice("decider", "jev-1.14", Native: true, "jev-1.14")
        );
        return model;
    }
}
