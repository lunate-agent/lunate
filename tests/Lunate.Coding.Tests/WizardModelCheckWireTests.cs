using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The model check through the real factory (B-4): the OpenCode Go preset declares
/// <c>x-opencode-session: {session.id}</c>, so a check built without a request identity throws
/// before the first byte. These tests run the real headers pipeline and assert the wire. The check
/// sends one short request capped at 16 output tokens, so every family's check request and first
/// session request match except for the prompt.
/// </summary>
public sealed class WizardModelCheckWireTests
{
    [Fact]
    public async Task The_model_check_sends_a_header_templated_preset_with_an_ephemeral_id()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire(OpenAiChatCompletion);
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));

        WizardCheckResult result = await WizardModelCheck.RunAsync(
            PresetWizard("opencode-go", "opencode-go/kimi-k3", "openai-chat"),
            catalog,
            wire.Factory(),
            AuthStore.Load(temp.File("auth.json"), _ => null),
            _ => null,
            () => "sk-test",
            context: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        HttpRequestMessage request = Assert.Single(wire.Requests);
        string session = Assert.Single(request.Headers.GetValues("x-opencode-session"));
        Assert.False(string.IsNullOrWhiteSpace(session));
        Assert.Equal(
            "https://opencode.ai/inference/go/openai/v1/chat/completions",
            request.RequestUri!.AbsoluteUri
        );
        string body = wire.Bodies[0];
        Assert.Contains("\"model\":\"kimi-k3\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("opencode-go/kimi-k3", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_model_check_names_the_sessions_id_when_one_exists()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire(OpenAiChatCompletion);
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));

        WizardCheckResult result = await WizardModelCheck.RunAsync(
            PresetWizard("opencode-go", "opencode-go/kimi-k3", "openai-chat"),
            catalog,
            wire.Factory(),
            AuthStore.Load(temp.File("auth.json"), _ => null),
            _ => null,
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        HttpRequestMessage request = Assert.Single(wire.Requests);
        Assert.Equal(
            "s_model-check",
            Assert.Single(request.Headers.GetValues("x-opencode-session"))
        );
    }

    [Theory]
    [InlineData("deepseek-v4.1-flash", "openai-chat", "chat-completions")]
    [InlineData("gpt-6-luna", "openai-responses", "responses")]
    [InlineData("claude-haiku-5-5", "anthropic-messages", "messages")]
    [InlineData("gemini-4-argon-contributor", "google", "gemini")]
    public async Task The_model_check_and_the_first_session_request_match_for_each_go_family(
        string wireId,
        string family,
        string response
    )
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire(ResponseFor(response));
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));
        AuthStore auth = AuthStore.Load(temp.File("auth.json"), _ => null);
        SetupWizardModel wizard = PresetWizard("opencode-go", $"opencode-go/{wireId}", family);

        await WizardModelCheck.RunAsync(
            wizard,
            catalog,
            wire.Factory(),
            auth,
            _ => null,
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        ResolvedModelProfile profile = ModelProfileResolver.Resolve(
            new ModelProfile("work", "opencode-go", $"opencode-go/{wireId}", ApiFamily: family),
            [SetupWizardCommit.StagedEntry(wizard)],
            catalog
        );
        IChatClient session = DecisionUsageModels.CreateUsageClient(
            profile,
            auth,
            _ => null,
            wire.Factory(),
            new RequestContext("s_model-check"),
            credentialOverride: "sk-test"
        );
        await session.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "the first session prompt")],
            new ChatOptions { MaxOutputTokens = 16 },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, wire.Requests.Count);
        Assert.Equal(
            wire.Requests[0].RequestUri!.AbsoluteUri,
            wire.Requests[1].RequestUri!.AbsoluteUri
        );
        Assert.Equal(HeaderText(wire.Requests[0]), HeaderText(wire.Requests[1]));
        Assert.Equal(WithoutPrompt(wire.Bodies[0]), WithoutPrompt(wire.Bodies[1]));
    }

    [Fact]
    public async Task The_model_check_and_the_first_session_request_match_for_a_custom_provider()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire(OpenAiChatCompletion);
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));
        AuthStore auth = AuthStore.Load(temp.File("auth.json"), _ => null);
        SetupWizardModel wizard = CustomWizard(
            "relay",
            "http://127.0.0.1:11434/v1",
            "test-model",
            "openai-chat"
        );

        await WizardModelCheck.RunAsync(
            wizard,
            catalog,
            wire.Factory(),
            auth,
            _ => null,
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        var custom = new CustomProvider(
            "relay",
            new Uri("http://127.0.0.1:11434/v1"),
            "test-model",
            "relay/test-model",
            ProviderApiFamily.OpenAiChat,
            CredentialPlacement.Bearer
        );
        ModelCatalog sessionCatalog = catalog.WithAdditionalModels([custom.ToModelInfo()]);
        ResolvedModelProfile profile = ModelProfileResolver.Resolve(
            new ModelProfile("work", "relay", "relay/test-model", ApiFamily: "openai-chat"),
            [SetupWizardCommit.StagedEntry(wizard)],
            sessionCatalog
        );
        IChatClient session = DecisionUsageModels.CreateUsageClient(
            profile,
            auth,
            _ => null,
            wire.Factory(),
            new RequestContext("s_model-check"),
            credentialOverride: "sk-test"
        );
        await session.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "the first session prompt")],
            new ChatOptions { MaxOutputTokens = 16 },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, wire.Requests.Count);
        Assert.Equal(
            wire.Requests[0].RequestUri!.AbsoluteUri,
            wire.Requests[1].RequestUri!.AbsoluteUri
        );
        Assert.Equal(HeaderText(wire.Requests[0]), HeaderText(wire.Requests[1]));
        Assert.Equal(WithoutPrompt(wire.Bodies[0]), WithoutPrompt(wire.Bodies[1]));
    }

    [Fact]
    public async Task A_compatible_openai_chat_check_sends_max_tokens()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire(OpenAiChatCompletion);
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));

        WizardCheckResult result = await WizardModelCheck.RunAsync(
            PresetWizard("opencode-go", "opencode-go/deepseek-v4.1-flash", "openai-chat"),
            catalog,
            wire.Factory(),
            AuthStore.Load(temp.File("auth.json"), _ => null),
            _ => null,
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        string body = Assert.Single(wire.Bodies);
        Assert.Contains("\"max_tokens\":16", body, StringComparison.Ordinal);
        Assert.DoesNotContain("max_completion_tokens", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_native_openai_chat_check_sends_max_completion_tokens()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire(OpenAiChatCompletion);
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));

        WizardCheckResult result = await WizardModelCheck.RunAsync(
            PresetWizard("openai", "gpt-4o", "openai-chat"),
            catalog,
            wire.Factory(),
            AuthStore.Load(temp.File("auth.json"), _ => null),
            _ => null,
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        string body = Assert.Single(wire.Bodies);
        Assert.Contains("\"max_completion_tokens\":16", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"max_tokens\"", body, StringComparison.Ordinal);
    }

    private static SetupWizardModel PresetWizard(string providerId, string modelId, string family)
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                providerId,
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: family,
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        wizard.RecordCredential(new WizardCredentialChoice(providerId, Key: "sk-test"));
        wizard.RecordModel(new WizardModelChoice(modelId, family));
        return wizard;
    }

    private static SetupWizardModel CustomWizard(
        string providerId,
        string endpoint,
        string modelId,
        string family
    )
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                providerId,
                Custom: true,
                Region: null,
                Endpoint: new Uri(endpoint),
                ApiFamily: family,
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        wizard.RecordCredential(new WizardCredentialChoice(providerId, Key: "sk-test"));
        wizard.RecordModel(new WizardModelChoice(modelId, family));
        return wizard;
    }

    private static string ResponseFor(string response) =>
        response switch
        {
            "responses" => ResponsesCompletion,
            "messages" => AnthropicCompletion,
            "gemini" => GeminiCompletion,
            _ => OpenAiChatCompletion,
        };

    private static string HeaderText(HttpRequestMessage request) =>
        string.Join(
            "\n",
            request
                .Headers.OrderBy(header => header.Key, StringComparer.Ordinal)
                .Select(header => $"{header.Key}: {string.Join(", ", header.Value)}")
        );

    private static string WithoutPrompt(string json)
    {
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        root.Remove("messages");
        root.Remove("input");
        root.Remove("contents");
        return root.ToJsonString();
    }

    private const string OpenAiChatCompletion = """
        {
          "id": "chatcmpl-model-check",
          "object": "chat.completion",
          "created": 0,
          "model": "wire-model",
          "choices": [
            {
              "index": 0,
              "message": { "role": "assistant", "content": "OK" },
              "finish_reason": "stop"
            }
          ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;

    private const string ResponsesCompletion = """
        {"id":"resp_1","object":"response","created_at":0,"status":"completed","model":"wire-model","output":[{"id":"msg_1","type":"message","status":"completed","role":"assistant","content":[{"type":"output_text","text":"OK","annotations":[]}]}],"usage":{"input_tokens":1,"output_tokens":1,"total_tokens":2}}
        """;

    private const string AnthropicCompletion = """
        {"id":"msg_1","type":"message","role":"assistant","model":"wire-model","content":[{"type":"text","text":"OK"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}
        """;

    private const string GeminiCompletion = """
        {"candidates":[{"content":{"parts":[{"text":"OK"}],"role":"model"},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":5,"candidatesTokenCount":1,"totalTokenCount":6}}
        """;

    /// <summary>The wire the factory's transport finally reaches: every request, in order.</summary>
    private sealed class CapturedWire(string response)
    {
        private readonly List<HttpRequestMessage> _requests = [];
        private readonly List<string> _bodies = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        public IReadOnlyList<string> Bodies => _bodies;

        public ChatClientFactory Factory()
        {
            var factory = new ChatClientFactory(
                NullLoggerFactory.Instance,
                enableOpenTelemetry: false
            );
            factory.BaseTransportFactory = () => new CaptureHandler(_requests, _bodies, response);
            return factory;
        }

        private sealed class CaptureHandler(
            List<HttpRequestMessage> requests,
            List<string> bodies,
            string response
        ) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                string body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                lock (requests)
                {
                    requests.Add(request);
                    bodies.Add(body);
                }

                return (
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(response, Encoding.UTF8, "application/json"),
                    }
                );
            }
        }
    }
}
