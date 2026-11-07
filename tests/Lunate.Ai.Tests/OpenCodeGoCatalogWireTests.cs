using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// OpenCode Go's built-in catalog: every entry names the native family the live listing reports,
/// and a model resolved straight from the catalog — no discovery, no profile override — routes
/// that family's own URL.
/// </summary>
[Collection(ChatClientFactoryEnvCollection.Name)]
public sealed class OpenCodeGoCatalogWireTests
{
    private const string OpenCodeApiKeyVariable = "OPENCODE_API_KEY";

    [Theory]
    [InlineData("deepseek-v4.1-flash", ProviderApiFamilies.OpenAiChat)]
    [InlineData("gpt-6-luna", ProviderApiFamilies.OpenAiResponses)]
    [InlineData("claude-haiku-5-5", ProviderApiFamilies.AnthropicMessages)]
    [InlineData("gemini-4-argon-contributor", ProviderApiFamilies.Google)]
    public void A_go_catalog_entry_carries_its_native_family(string wireId, string family)
    {
        ModelInfo model = ModelCatalog.Load().Find($"opencode-go/{wireId}")!;

        Assert.Equal(family, model.ApiFamily);
    }

    [Fact]
    public void Every_go_catalog_entry_names_a_known_family()
    {
        ModelInfo[] models =
        [
            .. ModelCatalog.Load().Models.Where(model => model.Provider == "opencode-go"),
        ];

        Assert.NotEmpty(models);
        Assert.All(
            models,
            model =>
            {
                Assert.True(ProviderApiFamilies.IsKnown(model.ApiFamily), model.Id);
                Assert.True(
                    model.ApiFamily == ProviderApiFamilies.SystemOne
                        ? model.Id == "opencode-go/jev-1.13"
                        : model.ApiFamily != ProviderApiFamilies.SystemOne,
                    model.Id
                );
            }
        );
        Assert.Equal(
            ["anthropic-messages", "google", "openai-chat", "openai-responses", "systemone"],
            models.Select(model => model.ApiFamily!).Distinct().Order()
        );
    }

    [Fact]
    public async Task A_catalog_chat_model_without_discovery_routes_the_go_chat_url()
    {
        using var environment = new EnvironmentScope((OpenCodeApiKeyVariable, "go-catalog-key"));
        CapturingWire wire = CapturingWire.OpenAi();
        ModelInfo model = ModelCatalog.Load().Find("opencode-go/deepseek-v4.1-flash")!;
        Assert.Null(model.Endpoint);

        IChatClient client = wire.Factory().Create(model, new RequestContext("go-catalog-session"));
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "https://opencode.ai/inference/go/openai/v1/chat/completions",
            wire.Last!.RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    public async Task A_catalog_responses_model_without_discovery_routes_the_go_responses_url()
    {
        using var environment = new EnvironmentScope((OpenCodeApiKeyVariable, "go-catalog-key"));
        var handler = new ScriptedResponsesHandler(OpenAiResponsesWirePayloads.TextTurn);
        ModelInfo model = ModelCatalog.Load().Find("opencode-go/gpt-6-luna")!;
        Assert.Null(model.Endpoint);

        IChatClient client = Factory(handler)
            .Create(model, new RequestContext("go-catalog-session"));
        await foreach (
            ChatResponseUpdate _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Hi")],
                options: null,
                TestContext.Current.CancellationToken
            )
        ) { }

        Assert.Equal(
            "https://opencode.ai/inference/go/openai/v1/responses",
            handler.Captures.Single().Request.RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    public async Task A_catalog_messages_model_without_discovery_routes_the_go_messages_url()
    {
        using var environment = new EnvironmentScope((OpenCodeApiKeyVariable, "go-catalog-key"));
        CapturingWire wire = CapturingWire.Anthropic();
        ModelInfo model = ModelCatalog.Load().Find("opencode-go/claude-haiku-5-5")!;
        Assert.Null(model.Endpoint);

        IChatClient client = wire.Factory().Create(model, new RequestContext("go-catalog-session"));
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "https://opencode.ai/inference/go/anthropic/v1/messages",
            wire.Last!.RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    public async Task A_catalog_google_model_without_discovery_routes_the_go_google_url()
    {
        using var environment = new EnvironmentScope((OpenCodeApiKeyVariable, "go-catalog-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ModelInfo model = ModelCatalog.Load().Find("opencode-go/gemini-4-argon-contributor")!;
        Assert.Null(model.Endpoint);

        IChatClient client = Factory(handler)
            .Create(model, new RequestContext("go-catalog-session"));
        await foreach (
            ChatResponseUpdate _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Hi")],
                options: null,
                TestContext.Current.CancellationToken
            )
        ) { }

        Assert.Equal("opencode.ai", handler.Captures.Single().Request.RequestUri!.Host);
        Assert.Equal(
            "/inference/go/google/v1beta/models/gemini-4-argon-contributor:streamGenerateContent",
            handler.Captures.Single().Request.RequestUri!.AbsolutePath
        );
    }

    private static ChatClientFactory Factory(HttpMessageHandler handler) =>
        new(new MarkingLoggerFactory(static () => { }), enableOpenTelemetry: false)
        {
            BaseTransportFactory = () => handler,
        };
}
