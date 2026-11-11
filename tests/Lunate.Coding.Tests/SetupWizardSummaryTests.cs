using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The summary's content (spec: The full walk, step 9): every collected value — region, the
/// model-check result, the decision profile's kind, the routing descriptions — and every skipped
/// step is named, and a model check that was skipped or failed labels the model unverified even
/// when discovery passed.
/// </summary>
[Trait("Category", "SetupWizardSummary")]
public sealed class SetupWizardSummaryTests
{
    [Fact]
    public void A_walked_configuration_renders_every_collected_value()
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                "local",
                Custom: true,
                Region: "eu-central",
                new Uri("https://local.example.test/v1"),
                "anthropic-messages",
                CredentialPlacement.XApiKey,
                new Uri("https://local.example.test/v1/models")
            )
        );
        wizard.RecordCredential(new WizardCredentialChoice("env:LOCAL_API_KEY"));
        wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Passed));
        wizard.RecordModel(new WizardModelChoice("claude-sonnet-5-5", "anthropic-messages"));
        wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Failed, "HTTP 404"));
        wizard.RecordChatProfile(new WizardProfileChoice("main", "claude-sonnet-5-5", "high"));
        wizard.RecordFastProfile(true);
        wizard.RecordDecisionProfile(
            new WizardUsageChoice("decisions", "local", "jev-1.14", "anthropic-messages")
        );
        wizard.RecordDecisions(
            new WizardDecisionsChoice(
                "shadow",
                "on",
                "on",
                ["main", "decisions"],
                new Dictionary<string, string>
                {
                    ["main"] = "everyday work, not audits; an example.",
                }
            )
        );

        IReadOnlyList<string> lines = SetupWizardSummary.Render(wizard);
        string text = string.Join("\n", lines);

        Assert.Contains("provider: local", text, StringComparison.Ordinal);
        Assert.Contains("region eu-central", text, StringComparison.Ordinal);
        Assert.Contains("family anthropic-messages", text, StringComparison.Ordinal);
        Assert.Contains("placement x-api-key", text, StringComparison.Ordinal);
        Assert.Contains(
            "models URL https://local.example.test/v1/models",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("credential: env:LOCAL_API_KEY", text, StringComparison.Ordinal);
        Assert.Contains("connection test: passed", text, StringComparison.Ordinal);
        Assert.Contains(
            "model: claude-sonnet-5-5 (family anthropic-messages)",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "model check: failed (HTTP 404) — the model stays unverified",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("chat profile: main (thinking high)", text, StringComparison.Ordinal);
        Assert.Contains("fast usage: main", text, StringComparison.Ordinal);
        Assert.Contains("decision profile: decisions (adapter", text, StringComparison.Ordinal);
        Assert.Contains("decisions.approvals: shadow", text, StringComparison.Ordinal);
        Assert.Contains(
            "routing description (main): everyday work, not audits; an example.",
            text,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_skipped_model_check_labels_the_model_unverified()
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                "openai-chat",
                CredentialPlacement.Bearer,
                null
            )
        );
        wizard.RecordCredential(new WizardCredentialChoice("OPENAI_API_KEY"));
        wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Passed));
        wizard.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        // Discovery passed; the model check was skipped — that never becomes "verified".
        wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Skipped));

        string text = string.Join("\n", SetupWizardSummary.Render(wizard));

        Assert.Contains("connection test: passed", text, StringComparison.Ordinal);
        Assert.Contains(
            "model check: skipped — the model stays unverified",
            text,
            StringComparison.Ordinal
        );
        // Discovery success alone never counts as a verified model.
        Assert.DoesNotContain("model check: passed", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skipped_steps_are_named_and_no_value_is_invented_for_them()
    {
        var wizard = new SetupWizardModel();
        wizard.Skip(SetupStep.Credential);
        wizard.Skip(SetupStep.Model);
        wizard.Skip(SetupStep.FastProfile);
        wizard.Skip(SetupStep.DecisionProfile);

        string text = string.Join("\n", SetupWizardSummary.Render(wizard));

        Assert.Contains(
            "skipped: credential, model, fast usage, decision profile",
            text,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("provider:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("model: ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_typed_custom_entry_names_the_settings_and_auth_files()
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                "homelab",
                Custom: true,
                Region: null,
                new Uri("http://localhost:8080/v1"),
                "openai-chat",
                CredentialPlacement.Bearer,
                null
            )
        );
        wizard.RecordCredential(new WizardCredentialChoice("homelab", Key: "typed-secret"));
        wizard.RecordModel(new WizardModelChoice("local-model", "openai-chat"));

        string text = string.Join("\n", SetupWizardSummary.Render(wizard));

        Assert.Contains(
            "entry settings.json; credential auth.json",
            text,
            StringComparison.Ordinal
        );
        Assert.Contains("(written to auth.json)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("typed-secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_native_decision_profile_names_its_pinned_version()
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                "openrouter",
                Custom: false,
                Region: null,
                Endpoint: null,
                "openai-chat",
                CredentialPlacement.Bearer,
                null
            )
        );
        wizard.RecordModel(new WizardModelChoice("anthropic/claude-sonnet-5-5", "openai-chat"));
        wizard.RecordDecisionProfile(
            new WizardUsageChoice(
                "decisions",
                "openrouter",
                "openrouter/typesafe/jev-1.14",
                ProviderApiFamilies.SystemOne,
                Native: true,
                PinnedVersion: "openrouter/typesafe/jev-1.14"
            )
        );

        string text = string.Join("\n", SetupWizardSummary.Render(wizard));

        Assert.Contains(
            "decision profile: decisions (native, pinned openrouter/typesafe/jev-1.14)",
            text,
            StringComparison.Ordinal
        );
    }
}
