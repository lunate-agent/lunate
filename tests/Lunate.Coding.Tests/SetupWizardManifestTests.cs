using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The pre-write manifest and the post-write receipt (spec: The file manifest and the receipt):
/// every staged value maps to the exact file the commit writes, a typed key is never printed, the
/// unchanged block names what the walk left alone, and a no-op reports no file.
/// </summary>
[Trait("Category", "SetupWizardSummary")]
public sealed class SetupWizardManifestTests
{
    private const string Secret = "manifest-test-key-never-printed";

    [Fact]
    public void A_rich_walk_maps_every_staged_write_to_its_file()
    {
        SetupWizardModel wizard = RichWalk();

        IReadOnlyList<SetupWrite> writes = SetupWizardManifest.Writes(wizard);

        Assert.Equal(
            [
                new(
                    SetupFile.Settings,
                    "provider local",
                    "base URL (anthropic-messages)",
                    "https://local.example.test/v1"
                ),
                new(
                    SetupFile.Settings,
                    "provider local",
                    "credential placement (anthropic-messages)",
                    "x-api-key"
                ),
                new(SetupFile.Settings, "provider local", "region", "eu-central"),
                new(
                    SetupFile.Settings,
                    "provider local",
                    "models URL",
                    "https://local.example.test/v1/models"
                ),
                new(SetupFile.Settings, "provider local", "model", "claude-sonnet-5-5"),
                new(
                    SetupFile.Auth,
                    "key local",
                    "stored",
                    "the key you typed — the value is never shown"
                ),
                new(SetupFile.Settings, "profile main", "profile", "main"),
                new(SetupFile.Settings, "profile main", "provider", "local"),
                new(SetupFile.Settings, "profile main", "model", "claude-sonnet-5-5"),
                new(SetupFile.Settings, "profile main", "reasoning level", "high"),
                new(SetupFile.Settings, "profile main", "api family", "anthropic-messages"),
                new(SetupFile.Settings, "profile main", "description", "everyday work, not audits"),
                new(SetupFile.Settings, "usage chat", "profile", "main"),
                new(SetupFile.Settings, "usage fast", "profile", "main"),
                new(SetupFile.Settings, "profile decisions", "profile", "decisions"),
                new(SetupFile.Settings, "profile decisions", "provider", "local"),
                new(SetupFile.Settings, "profile decisions", "model", "jev-1.14"),
                new(SetupFile.Settings, "profile decisions", "api family", "anthropic-messages"),
                new(SetupFile.Settings, "profile decisions", "description", "audits only"),
                new(SetupFile.Settings, "decisions", "profile", "decisions"),
                new(SetupFile.Settings, "decisions", "approvals", "shadow"),
                new(SetupFile.Settings, "decisions", "routing", "on"),
                new(SetupFile.Settings, "decisions", "routing profiles", "main, decisions"),
                new(SetupFile.Settings, "decisions", "tool", "on"),
                new(SetupFile.Settings, "decisions", "questions", "suggest"),
            ],
            writes
        );
    }

    [Fact]
    public void The_manifest_names_only_the_routing_profiles_the_multi_select_confirmed()
    {
        // Three candidates were offered (the walk's chat and decision profiles plus the settings
        // file's existing 'nightly'); the multi-select confirmed two. The manifest must name what
        // the commit will write, not every candidate it was offered.
        SetupWizardModel wizard = RichWalk();
        wizard.ExistingRoutingProfiles = ["nightly"];

        IReadOnlyList<SetupWrite> writes = SetupWizardManifest.Writes(wizard);

        Assert.Contains(
            new(SetupFile.Settings, "decisions", "routing profiles", "main, decisions"),
            writes
        );
        Assert.DoesNotContain(
            writes,
            write => write.Value.Contains("nightly", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void A_preset_reference_walk_writes_the_provider_file_and_no_key()
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
        wizard.RecordCredential(new WizardCredentialChoice("env:OPENAI_API_KEY"));
        wizard.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        wizard.RecordChatProfile(new WizardProfileChoice("main", "gpt-5", null));

        IReadOnlyList<SetupWrite> writes = SetupWizardManifest.Writes(wizard);

        Assert.Contains(
            new(
                SetupFile.Providers,
                "provider openai",
                "credential reference",
                "env:OPENAI_API_KEY"
            ),
            writes
        );
        Assert.Contains(new(SetupFile.Settings, "usage chat", "profile", "main"), writes);
        Assert.DoesNotContain(writes, write => write.File == SetupFile.Auth);
        Assert.DoesNotContain(
            writes,
            write => write.Value.Contains(Secret, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void The_manifest_and_receipt_never_print_a_typed_key()
    {
        SetupWizardModel wizard = RichWalk();

        string manifest = string.Join("\n", SetupWizardManifest.Render(wizard, 40));
        string receipt = string.Join("\n", SetupWizardManifest.RenderReceipt(wizard, 40));

        Assert.DoesNotContain(Secret, manifest, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, receipt, StringComparison.Ordinal);
        Assert.Contains("key local", manifest, StringComparison.Ordinal);
        Assert.Contains("auth.json", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void The_unchanged_block_names_the_defaults_and_where_to_change_them()
    {
        var wizard = new SetupWizardModel();
        wizard.Skip(SetupStep.Provider);
        wizard.Skip(SetupStep.DecisionProfile);
        wizard.RecordDecisions(null);

        IReadOnlyList<string> lines = SetupWizardManifest.Render(wizard, 40);
        string text = string.Join("\n", lines);

        Assert.Contains("left at default / unchanged", text, StringComparison.Ordinal);
        Assert.Contains("provider:", text, StringComparison.Ordinal);
        Assert.Contains("/setup", text, StringComparison.Ordinal);
        Assert.Contains("~/.lunate/providers.json", text, StringComparison.Ordinal);
        Assert.Contains("decisions.approvals:", text, StringComparison.Ordinal);
        Assert.Contains("/settings", text, StringComparison.Ordinal);
        Assert.Contains("~/.lunate/settings.json", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_manifest_and_the_receipt_fit_forty_columns()
    {
        SetupWizardModel wizard = RichWalk();

        foreach (
            string surface in new[]
            {
                string.Join("\n", SetupWizardManifest.Render(wizard, 40)),
                string.Join("\n", SetupWizardManifest.RenderReceipt(wizard, 40)),
            }
        )
        {
            Assert.All(
                surface.Split('\n'),
                line => Assert.True(CellText.Width(line) <= 40, $"'{line}' overflows 40 cells")
            );
        }
    }

    [Fact]
    public void A_no_op_receipt_names_no_file()
    {
        var wizard = new SetupWizardModel();
        foreach (SetupStep step in SetupWizardModel.Order)
        {
            wizard.Enter(step);
            wizard.Skip(step);
        }

        string receipt = string.Join("\n", SetupWizardManifest.RenderReceipt(wizard, 40));

        Assert.Contains("nothing — every step was skipped", receipt, StringComparison.Ordinal);
        Assert.DoesNotContain("providers.json", receipt, StringComparison.Ordinal);
        Assert.DoesNotContain("auth.json", receipt, StringComparison.Ordinal);
        Assert.DoesNotContain("settings.json", receipt, StringComparison.Ordinal);
    }

    private static SetupWizardModel RichWalk()
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
        wizard.RecordCredential(new WizardCredentialChoice("local", Key: Secret));
        wizard.RecordModel(new WizardModelChoice("claude-sonnet-5-5", "anthropic-messages"));
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
                    ["main"] = "everyday work, not audits",
                    ["decisions"] = "audits only",
                },
                "suggest"
            )
        );
        return wizard;
    }
}
