using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lunate.Coding.Tests;

/// <summary>
/// The decision-profile model check (spec: The full walk, step 5 — "one tiny noul for a decision
/// profile"). The native profile rides the real System One transport over a fake handler, so the
/// pinned request is the wire body a provider would receive; the adapter profile rides the chat
/// client under the decision layer's adapter. No test reaches a live endpoint.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardDecisionCheckTests : IDisposable
{
    private const string JevModel = "openrouter/typesafe/jev-1.14";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task The_native_check_sends_one_tiny_noul_through_the_systemone_transport()
    {
        using var handler = new DecisionTransportHandler(
            """{"model":"typesafe/jev-1.14","answers":{"transport_reachable":{"type":"noul","noul":0.91}}}"""
        );
        SetupWizardModel wizard = NativeWalk(
            new WizardCredentialChoice("openrouter", Key: "walk-key")
        );

        WizardCheckResult result = await RunAsync(wizard, handler, credential: "walk-key");

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("https://openrouter.ai/api/v1/systemone", handler.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer walk-key", handler.Authorization);
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody);
        JsonElement root = body.RootElement;
        // The wire model id, never the catalog id.
        Assert.Equal("typesafe/jev-1.14", root.GetProperty("model").GetString());
        Assert.Equal("{}", root.GetProperty("state").GetRawText());
        JsonElement question = Assert.Single(root.GetProperty("questions").EnumerateObject()).Value;
        Assert.Equal("noul", question.GetProperty("type").GetString());
        Assert.Equal(
            "Answer yes when the decision transport answered this request.",
            question.GetProperty("instructions").GetString()
        );
        Assert.Null(handler.SessionHeader);
    }

    [Fact]
    public async Task The_opencode_go_native_check_passes_and_carries_the_session_header()
    {
        using var handler = new DecisionTransportHandler(
            """{"model":"jev-1.13","answers":{"transport_reachable":{"type":"noul","noul":0.91}}}"""
        );
        SetupWizardModel wizard = NativeWalk(
            new WizardCredentialChoice("opencode-go", Key: "walk-key"),
            provider: "opencode-go",
            model: "opencode-go/jev-1.13",
            decisionModel: "opencode-go/jev-1.13"
        );

        WizardCheckResult result = await RunAsync(wizard, handler, credential: "walk-key");

        Assert.True(
            result.Outcome == WizardProgress.Passed,
            result.Detail ?? "the decision check failed without a reason"
        );
        Assert.Equal(
            "https://opencode.ai/inference/systemone/v1/systemone",
            handler.RequestUri!.AbsoluteUri
        );
        Assert.False(string.IsNullOrWhiteSpace(handler.SessionHeader));
        using JsonDocument body = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("jev-1.13", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task A_native_check_failure_reports_the_status_and_never_the_key()
    {
        using var handler = new DecisionTransportHandler(
            """{"error":{"message":"model not available"}}""",
            HttpStatusCode.BadRequest
        );
        SetupWizardModel wizard = NativeWalk(new WizardCredentialChoice("env:OPENROUTER_API_KEY"));

        WizardCheckResult result = await RunAsync(wizard, handler, credential: "env-walk-key");

        Assert.Equal(WizardProgress.Failed, result.Outcome);
        Assert.Contains("400", result.Detail ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "env-walk-key",
            result.Detail ?? string.Empty,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task The_adapter_check_sends_the_decision_request_not_a_chat_completion()
    {
        var client = new RecordingDecisionChatClient(
            """{"results":{"transport_reachable":{"answer":"yes","probabilities":{"yes":0.9,"no":0.1}}}}"""
        );
        var factory = new FakeChatClientFactory(client);
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
        wizard.RecordDecisionProfile(
            new WizardUsageChoice("decisions", "openai", "gpt-5", "openai-chat")
        );

        WizardCheckResult result = await WizardModelCheck.RunDecisionAsync(
            wizard,
            ModelCatalog.Load(null),
            factory,
            AuthStore.Load(_temp.File("auth.json")),
            name => name == "OPENAI_API_KEY" ? "adapter-walk-key" : null,
            () => "adapter-walk-key",
            context: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        Assert.Single(client.Requests);
        Assert.Equal(ChatResponseFormat.Json, client.Options[0]!.ResponseFormat);
        string user = client.Requests[0][^1].Text;
        using JsonDocument payload = JsonDocument.Parse(user);
        JsonElement question = payload
            .RootElement.GetProperty("questions")
            .EnumerateArray()
            .Single();
        Assert.Equal("transport_reachable", question.GetProperty("id").GetString());
        Assert.Equal("yes/no", question.GetProperty("kind").GetString());
    }

    [Fact]
    public void The_summary_marks_a_skipped_or_failed_decision_check_unverified()
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
        wizard.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        wizard.RecordDecisionProfile(
            new WizardUsageChoice("decisions", "openai", "gpt-5", "openai-chat")
        );
        wizard.RecordDecisionCheck(new WizardCheckResult(WizardProgress.Skipped));

        string skipped = string.Join("\n", SetupWizardSummary.Render(wizard));
        Assert.Contains(
            "decision check: skipped — the decision profile stays unverified",
            skipped,
            StringComparison.Ordinal
        );

        wizard.RecordDecisionCheck(new WizardCheckResult(WizardProgress.Failed, "HTTP 404"));
        string failed = string.Join("\n", SetupWizardSummary.Render(wizard));
        Assert.Contains(
            "decision check: failed (HTTP 404) — the decision profile stays unverified",
            failed,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_summary_names_the_model_the_passed_decision_check_used()
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
        wizard.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        wizard.RecordDecisionProfile(
            new WizardUsageChoice(
                "decisions",
                "openai",
                "gpt-5",
                "openai-chat",
                Native: false,
                PinnedVersion: null
            )
        );
        wizard.RecordDecisionCheck(new WizardCheckResult(WizardProgress.Passed));

        string summary = string.Join("\n", SetupWizardSummary.Render(wizard));

        // The check's result and the model it used: the check line names the outcome, the
        // decision profile's own group names the model.
        Assert.Contains("decision check: passed", summary, StringComparison.Ordinal);
        Assert.Contains("profile decisions", summary, StringComparison.Ordinal);
        Assert.Contains("model: gpt-5", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_decision_profile_without_a_check_is_named_unverified()
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
        wizard.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        wizard.RecordDecisionProfile(
            new WizardUsageChoice("decisions", "openai", "gpt-5", "openai-chat")
        );

        string summary = string.Join("\n", SetupWizardSummary.Render(wizard));

        Assert.Contains(
            "decision check: not run — the decision profile stays unverified",
            summary,
            StringComparison.Ordinal
        );
    }

    [PosixFact]
    public async Task The_walk_runs_the_native_check_and_the_summary_records_it()
    {
        using var handler = new DecisionTransportHandler(
            """{"model":"typesafe/jev-1.14","answers":{"transport_reachable":{"type":"noul","noul":0.91}}}"""
        );
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        var factory = new ChatClientFactory(loggerFactory) { BaseTransportFactory = () => handler };
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        await PickRowAsync(host, "Select provider", "openrouter — OpenRouter");
        await host.WaitForFrameAsync("Credential for openrouter");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openrouter");
        host.Console.SendText("walk-decision-key");
        host.Console.SendEnter();
        await PickRowAsync(host, "Connection test", "Skip the connection test");
        await PickRowAsync(host, "Model (discovery listed nothing", "enter a model id manually");
        await host.WaitForFrameAsync("Model id");
        host.Console.SendText("deep/deepseek-chat");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // skip the chat check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-deep");
        host.Console.SendEnter();
        await PickRowAsync(host, "fast usage", "Skip");
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done
        await host.WaitForFrameAsync("Decision profile (the provider exposes the native Jev");
        host.Console.SendText("jev");
        await host.WaitForFrameAsync("> openrouter/typesafe/jev-1.14 (native Jev)");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decision check");
        await PressUpAsync(host);
        Assert.Contains("> Send the decision check", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendEnter(); // send the decision check

        // The check belongs to the decision-profile step, so the walk's next step opens only
        // once the request has been answered: waiting for it pins the order the summary reports.
        await host.WaitForFrameAsync("Decisions settings");
        Assert.True(
            handler.RequestCount == 1,
            $"the decision check sent {handler.RequestCount} requests; last frame:\n{host.LastFrame}"
        );
        host.Console.SendEnter(); // accept the default decisions settings

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        await host.WaitForFrameAsync("Write this configuration?");
        string flat = Flatten(summary);
        Assert.Contains("decision check: passed", flat, StringComparison.Ordinal);
        Assert.Contains("model: openrouter/typesafe/jev-1.14", flat, StringComparison.Ordinal);
        Assert.Contains("api family: systemone", flat, StringComparison.Ordinal);
        Assert.Equal(1, handler.RequestCount);

        host.Console.SendEscape(); // abort at the summary: nothing is written
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task The_decision_check_inside_a_setup_session_carries_that_sessions_id()
    {
        using var handler = new DecisionTransportHandler(
            """{"model":"jev-1.13","answers":{"transport_reachable":{"type":"noul","noul":0.91}}}"""
        );
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        var factory = new ChatClientFactory(loggerFactory) { BaseTransportFactory = () => handler };
        using var host = ConfiguredHost(factory);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        foreach (
            string row in new[]
            {
                "> Add a profile",
                "> Change the chat profile",
                "> Edit the decisions layer",
                "> Walk everything",
            }
        )
        {
            host.Console.Send(
                new Lunate.Tui.KeyEvent(Lunate.Tui.KeyKind.Down, null, false, false, false)
            );
            await host.WaitForFrameAsync(row);
        }

        host.Advance(500);
        host.Console.SendEnter();
        await host.ChooseDetailedModeAsync();
        await PickRowAsync(host, "Select provider", "opencode-go — OpenCode Go");
        await host.WaitForFrameAsync("Credential for opencode-go");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for opencode-go");
        host.Console.SendText("walk-decision-key");
        host.Console.SendEnter();
        await PickRowAsync(host, "Connection test", "Skip the connection test");
        await PickRowAsync(host, "Model (discovery listed nothing", "enter a model id manually");
        await host.WaitForFrameAsync("Model id");
        host.Console.SendText("any-model");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // skip the chat check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-go");
        host.Console.SendEnter();
        await PickRowAsync(host, "fast usage", "Skip");
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done
        await host.WaitForFrameAsync("Decision profile (the provider exposes the native Jev");
        // The flat list's row order follows the configured providers, so the native Jev row is
        // found by its own text instead of an assumed offset from Skip.
        host.Console.SendText("jev-1.13");
        await host.WaitForFrameAsync("> opencode-go/jev-1.13 (native Jev");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decision check");
        await PressUpAsync(host);
        Assert.Contains("> Send the decision check", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Decisions settings");
        string sessionId = SessionId(host);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(sessionId, handler.SessionHeader);

        host.Console.SendEscape(); // the menu is local: back to the decision-profile step
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEscape(); // a question's Esc ends the walk
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    /// <summary>Sends one Up press and waits for the picker's repaint.</summary>
    private static async Task PressUpAsync(InteractiveSessionHost host)
    {
        string previous = host.LastFrame;
        host.Console.SendUp();
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.LastFrame != previous;
        });
    }

    private static async Task PickRowAsync(InteractiveSessionHost host, string title, string row)
    {
        await host.WaitForFrameAsync(title);
        string previous = host.LastFrame;
        for (var step = 0; step < 24; step++)
        {
            if (host.LastFrame.Contains($"> {row}", StringComparison.Ordinal))
            {
                break;
            }

            host.Console.Send(
                new Lunate.Tui.KeyEvent(Lunate.Tui.KeyKind.Down, null, false, false, false)
            );
            await host.WaitUntilAsync(() =>
            {
                // The picker's repaint is scheduler-scheduled: advance the session's virtual
                // clock the way WaitForFrameAsync does, or the new frame never paints.
                host.Advance(33);
                return host.LastFrame != previous;
            });
            previous = host.LastFrame;
        }

        Assert.Contains($"> {row}", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendEnter();
    }

    private static string Flatten(string text) =>
        string.Join(
            " ",
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())
        );

    private static SetupWizardModel NativeWalk(
        WizardCredentialChoice credential,
        string provider = "openrouter",
        string model = "deepseek/deepseek-chat",
        string? decisionModel = null
    )
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                provider,
                Custom: false,
                Region: null,
                Endpoint: null,
                "openai-chat",
                CredentialPlacement.Bearer,
                null
            )
        );
        wizard.RecordCredential(credential);
        wizard.RecordModel(new WizardModelChoice(model, "openai-chat"));
        string decisionId = decisionModel ?? JevModel;
        wizard.RecordDecisionProfile(
            new WizardUsageChoice(
                "decisions",
                provider,
                decisionId,
                ProviderApiFamilies.SystemOne,
                Native: true,
                PinnedVersion: decisionId
            )
        );
        return wizard;
    }

    private static string SessionId(InteractiveSessionHost host) =>
        Session
            .Load(Assert.Single(Directory.GetFiles(host.Temp.File("sessions"), "*.jsonl")))
            .SessionId;

    /// <summary>A configured install, so `/setup` opens its entry menu instead of the first-run
    /// auth picker taking the keys.</summary>
    private static InteractiveSessionHost ConfiguredHost(ChatClientFactory factory)
    {
        var host = new InteractiveSessionHost(factory: factory, interactiveConsole: true);
        bool valid = ProviderEntry.TryCreate(
            "openai",
            "openai",
            new Uri("https://provider.example.test/v1"),
            "auth:openai",
            headers: null,
            region: null,
            out ProviderEntry? entry,
            out string? error
        );
        Assert.True(valid, error);
        ProviderStore.Save(AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")), [entry!]);
        File.WriteAllText(
            AuthStore.SettingsFilePath(host.Temp.File("auth.json")),
            """
            {
              "schemaVersion": 2,
              "model": "gpt-4.1",
              "profiles": { "chat-main": { "provider": "openai", "model": "gpt-4.1" } },
              "usages": { "chat": "chat-main" }
            }
            """
        );
        return host;
    }

    private Task<WizardCheckResult> RunAsync(
        SetupWizardModel wizard,
        HttpMessageHandler handler,
        string? credential,
        RequestContext? context = null
    ) =>
        WizardModelCheck.RunDecisionAsync(
            wizard,
            ModelCatalog.Load(null),
            new ChatClientFactory(LoggerFactory.Create(_ => { }))
            {
                BaseTransportFactory = () => handler,
            },
            AuthStore.Load(_temp.File("auth.json")),
            name => name == "OPENROUTER_API_KEY" ? "env-walk-key" : null,
            () => credential,
            context,
            TestContext.Current.CancellationToken
        );

    /// <summary>The one HTTP request the check makes, captured with its body and credential.</summary>
    private sealed class DecisionTransportHandler(
        string responseBody,
        HttpStatusCode status = HttpStatusCode.OK
    ) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;
        public string? Authorization { get; private set; }
        public string? SessionHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            SessionHeader = request.Headers.TryGetValues("x-opencode-session", out var sessions)
                ? sessions.Single()
                : null;
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    /// <summary>The chat client the adapter path calls, recording the decision request it got.</summary>
    private sealed class RecordingDecisionChatClient(string reply) : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
        public List<ChatOptions?> Options { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Requests.Add([.. messages]);
            Options.Add(options);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
