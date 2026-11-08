using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lunate.Coding.Tests;

/// <summary>
/// The fast and decision usages from any provider (spec: Fast and decision usages from any
/// provider): the flat option list spans every configured provider, and each of the three routes —
/// the provider configured in this run, another already-configured provider, a provider set up
/// inline — reaches a passing check, writes its profile/usage and keeps the answers it already had.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardUsageRoutesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    /// <summary>The reported symptom: a walk that configured openai could only pick openai models.
    /// The list now spans the other configured provider too, in both usages.</summary>
    [Fact]
    public void The_usage_list_spans_every_configured_provider()
    {
        SetupWizardModel wizard = OpenAiWalk();
        ProviderEntry openai = Entry("openai", "auth:openai");
        ProviderEntry anthropic = Entry("anthropic", "auth:anthropic");
        var catalog = ModelCatalog.Load(null);

        IReadOnlyList<string> fast = Labels(wizard, [openai, anthropic], catalog, decision: false);
        IReadOnlyList<string> decision = Labels(
            wizard,
            [openai, anthropic],
            catalog,
            decision: true
        );

        Assert.Contains(
            fast,
            label =>
                label.Contains("anthropic/claude-sonnet-5-5", StringComparison.Ordinal)
                && label.Contains("anthropic-messages", StringComparison.Ordinal)
        );
        Assert.Contains(fast, label => label.StartsWith("openai/", StringComparison.Ordinal));
        Assert.Contains(
            decision,
            label => label.Contains("anthropic/claude-sonnet-5-5", StringComparison.Ordinal)
        );
        // A fast usage never offers the decision-only family.
        Assert.DoesNotContain(fast, label => label.Contains("systemone", StringComparison.Ordinal));
    }

    /// <summary>A configured provider that serves System One contributes its native Jev rows to
    /// the decision list only, labelled native.</summary>
    [Fact]
    public void The_decision_list_offers_another_providers_native_jev_model()
    {
        SetupWizardModel wizard = OpenAiWalk();
        ProviderEntry opencodeGo = Entry("opencode-go", "auth:opencode-go");
        var catalog = ModelCatalog.Load(null);

        IReadOnlyList<string> decision = Labels(
            wizard,
            [Entry("openai", "auth:openai"), opencodeGo],
            catalog,
            decision: true
        );
        IReadOnlyList<string> fast = Labels(
            wizard,
            [Entry("openai", "auth:openai"), opencodeGo],
            catalog,
            decision: false
        );

        Assert.Contains(
            decision,
            label => label.Contains("opencode-go/jev-1.13 (native Jev", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            fast,
            label => label.Contains("native Jev", StringComparison.Ordinal)
        );
    }

    /// <summary>Route 1: the fast usage picks the chat model, so the chat profile also serves fast —
    /// no second profile and no second request; the chat model check is the usage's check.</summary>
    [Fact]
    public void Fast_from_the_provider_configured_in_this_run_reuses_the_chat_profile()
    {
        SetupWizardModel wizard = OpenAiWalk();
        wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Passed));
        wizard.RecordFastProfile(true);

        Assert.NotNull(wizard.FastUsage);
        Assert.True(wizard.FastUsage!.ReuseChatProfile);
        Assert.Equal("main", wizard.FastUsage.ProfileName);
        Assert.Equal(WizardProgress.Passed, wizard.ModelCheck!.Outcome);

        Commit(wizard, out AgentSettings settings);
        Assert.Equal("main", settings.Usages["chat"]);
        Assert.Equal("main", settings.Usages["fast"]);
        Assert.DoesNotContain("fast", settings.Profiles.Keys, StringComparer.Ordinal);
    }

    /// <summary>Route 2: a provider already configured on the machine supplies the fast model; the
    /// check resolves that provider's own entry and stored credential, and the commit writes the
    /// fast profile plus the usage.</summary>
    [Fact]
    public async Task Fast_from_another_configured_provider_writes_its_profile_and_usage()
    {
        AuthStore auth = AuthStore.Load(_temp.File("auth.json"), NoEnvironment);
        auth.Set("anthropic", "anthropic-key");
        auth.Save();
        ProviderEntry anthropic = Entry("anthropic", "auth:anthropic");
        WizardModelRef model = new(
            WizardUsageOptions.CarriedProvider(anthropic),
            Credential: null,
            anthropic,
            new WizardModelChoice("claude-sonnet-5-5", "anthropic-messages")
        );
        var factory = new RecordingFactory(new ScriptedChatClient().Enqueue(Scripts.Text("OK")));

        WizardCheckResult check = await WizardModelCheck.RunForAsync(
            model,
            ModelCatalog.Load(null),
            factory,
            auth,
            PrintModeTestSupport.Environment(),
            credentialOverride: null,
            context: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, check.Outcome);
        (ModelInfo created, string? key) = Assert.Single(factory.Created);
        Assert.Equal("claude-sonnet-5-5", created.Id);
        Assert.Equal("anthropic-key", key);

        SetupWizardModel wizard = OpenAiWalk();
        wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Passed));
        wizard.RecordFastUsage(
            new WizardUsageChoice(
                "fast",
                model.Provider.Id,
                model.Model.ModelId,
                model.Model.ApiFamily
            )
        );
        wizard.RecordFastCheck(check);

        Commit(wizard, out AgentSettings settings);
        Assert.Equal("fast", settings.Usages["fast"]);
        Assert.Equal("main", settings.Usages["chat"]);
        Assert.True(settings.Profiles.TryGetValue("fast", out ModelProfile? fast));
        Assert.Equal("anthropic", fast.Provider);
        Assert.Equal("claude-sonnet-5-5", fast.Model);
    }

    /// <summary>Route 3: the decision step sets up opencode-go inline, the native Jev profile binds
    /// it, the check goes through the System One transport, and one commit writes the provider, its
    /// typed key and the profile — with the earlier chat answer intact.</summary>
    [Fact]
    public async Task Decision_from_a_provider_set_up_inline_lands_in_the_same_review_and_commit()
    {
        SetupWizardModel wizard = OpenAiWalk();
        wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Passed));
        WizardProviderChoice provider = new(
            "opencode-go",
            Custom: false,
            Region: null,
            Endpoint: null,
            ProviderApiFamilies.Name(ProviderApiFamily.OpenAiChat),
            CredentialPlacement.Bearer,
            ModelsUrl: null
        );
        var credential = new WizardCredentialChoice("opencode-go", Key: "inline-go-key");
        var model = new WizardModelChoice("opencode-go/jev-1.13", ProviderApiFamilies.SystemOne);
        wizard.RecordInlineProvider(
            new WizardInlineProvider(
                provider,
                credential,
                new WizardCheckResult(WizardProgress.Skipped),
                model,
                []
            )
        );
        var usage = new WizardUsageChoice(
            "decisions",
            "opencode-go",
            "opencode-go/jev-1.13",
            ProviderApiFamilies.SystemOne,
            Native: true,
            PinnedVersion: "opencode-go/jev-1.13"
        );
        wizard.RecordDecisionProfile(usage);

        // The inline provider is in the very same review the walk shows.
        Assert.Contains(
            "added provider: opencode-go",
            string.Join("\n", SetupWizardSummary.Render(wizard)),
            StringComparison.Ordinal
        );
        Assert.Contains(
            WizardUsageOptions
                .Build(wizard, [], [], ModelCatalog.Load(null), decision: true)
                .Select(option => option.Label),
            label => label.Contains("opencode-go/jev-1.13 (native Jev", StringComparison.Ordinal)
        );

        using var handler = new UsageRouteHandler();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        AuthStore auth = AuthStore.Load(_temp.File("auth.json"), NoEnvironment);
        var reference = new WizardModelRef(
            provider,
            credential,
            SetupWizardCommit.StagedEntry(provider, credential),
            model,
            Native: true,
            PinnedVersion: "opencode-go/jev-1.13"
        );
        WizardCheckResult check = await WizardModelCheck.RunDecisionForAsync(
            reference,
            ModelCatalog.Load(null),
            new ChatClientFactory(loggerFactory) { BaseTransportFactory = () => handler },
            auth,
            PrintModeTestSupport.Environment(),
            credentialOverride: credential.Key,
            context: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, check.Outcome);
        Assert.Equal(1, handler.SystemOneRequests);
        wizard.RecordDecisionCheck(check);

        Commit(wizard, out AgentSettings settings);
        Assert.Equal("main", settings.Usages["chat"]);
        Assert.True(settings.Profiles.TryGetValue("main", out _));
        Assert.True(settings.Profiles.TryGetValue("decisions", out ModelProfile? decision));
        Assert.Equal("opencode-go", decision.Provider);
        Assert.Equal("opencode-go/jev-1.13", decision.Model);
        Assert.Equal(ProviderApiFamilies.SystemOne, decision.ApiFamily);
        Assert.Equal("decisions", settings.Decisions.Profile);

        AuthStore saved = AuthStore.Load(_temp.File("auth.json"), NoEnvironment);
        Assert.Equal("inline-go-key", saved.TryGet("opencode-go"));
        ProviderEntry[] entries =
        [
            .. ProviderStore.Load(_temp.File("providers.json"), saved, []).Entries,
        ];
        Assert.Contains(entries, entry => entry.Name == "opencode-go");
    }

    private static IReadOnlyList<string> Labels(
        SetupWizardModel wizard,
        IReadOnlyList<ProviderEntry> configured,
        ModelCatalog catalog,
        bool decision
    ) =>
        [
            .. WizardUsageOptions
                .Build(wizard, configured, [], catalog, decision)
                .Select(option => option.Label),
        ];

    /// <summary>A walk that configured openai and named its chat profile, before any usage answer.</summary>
    private static SetupWizardModel OpenAiWalk()
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(OpenAi());
        wizard.RecordCredential(new WizardCredentialChoice("auth:openai"));
        wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Passed));
        wizard.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        wizard.RecordChatProfile(new WizardProfileChoice("main", "gpt-5", ReasoningLevel: null));
        return wizard;
    }

    private static WizardProviderChoice OpenAi() =>
        new(
            "openai",
            Custom: false,
            Region: null,
            Endpoint: null,
            "openai-chat",
            CredentialPlacement.Bearer,
            ModelsUrl: null
        );

    /// <summary>The machine's real environment is never part of a fixture: a live provider key
    /// would override the store and make the assertion agree with the wrong source.</summary>
    private static string? NoEnvironment(string name) => null;

    private static ProviderEntry Entry(string name, string credential)
    {
        bool valid = ProviderEntry.TryCreate(
            name,
            name,
            new Uri($"https://{name}.example.test/v1"),
            credential,
            headers: null,
            region: null,
            out ProviderEntry? entry,
            out string? error
        );
        Assert.True(valid, error);
        return entry!;
    }

    private void Commit(SetupWizardModel wizard, out AgentSettings settings)
    {
        string settingsPath = _temp.File("settings.json");
        string providerPath = _temp.File("providers.json");
        AuthStore auth = AuthStore.Load(_temp.File("auth.json"), NoEnvironment);
        SetupWizardCommit.Commit(
            wizard,
            new SetupWizardCommit.CommitPaths(settingsPath, providerPath),
            () => auth
        );
        settings = SettingsStore.Resolve(settingsPath);
    }

    /// <summary>Records the created model and the explicit credential the check resolved.</summary>
    private sealed class RecordingFactory(IChatClient client) : IChatClientFactory
    {
        public List<(ModelInfo Model, string? ApiKey)> Created { get; } = [];

        public IChatClient Create(ModelInfo model)
        {
            Created.Add((model, null));
            return client;
        }

        public IChatClient Create(ModelInfo model, string? apiKey)
        {
            Created.Add((model, apiKey));
            return client;
        }
    }

    /// <summary>The System One transport for the inline decision check; a chat request would be a
    /// different shape and is answered separately so the route cannot silently take the adapter
    /// path.</summary>
    private sealed class UsageRouteHandler : HttpMessageHandler
    {
        public int SystemOneRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            _ = await request.Content!.ReadAsStringAsync(cancellationToken);
            bool systemOne = request.RequestUri!.AbsolutePath.EndsWith(
                "/systemone",
                StringComparison.Ordinal
            );
            if (systemOne)
            {
                SystemOneRequests++;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    systemOne
                        ? """{"model":"jev-1.13","answers":{"transport_reachable":{"type":"noul","noul":0.91}}}"""
                        : """{"choices":[{"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}]}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
