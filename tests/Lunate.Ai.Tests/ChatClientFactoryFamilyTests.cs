using System.Reflection;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// The client factory's family routing: adapter selection by family, and the failure an
/// unanswerable two-family provider (or an unknown family value) produces instead of silently
/// using chat.
/// </summary>
[Collection(ChatClientFactoryEnvCollection.Name)]
public sealed class ChatClientFactoryFamilyTests
{
    private const string OpenAiApiKeyVariable = "OPENAI_API_KEY";
    private const string OpenCodeApiKeyVariable = "OPENCODE_API_KEY";

    [Fact]
    public void A_decision_only_family_reaching_the_factory_is_refused_naming_the_family_and_the_wired_set()
    {
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        var model = new ModelInfo("m", "openrouter", Endpoint: null, ContextWindow: 8192, true)
        {
            ApiFamily = "systemone",
        };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            factory.Create(model, "fixture-only-key")
        );

        Assert.Contains("'systemone'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("only 'openai-chat'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'google' have clients", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("openai-chat", "OpenAIChatClient")]
    [InlineData("openai-responses", "OpenAIResponsesChatClient")]
    [InlineData("anthropic-messages", "Anthropic")]
    [InlineData("google", "GoogleGenAIChatClient")]
    public void A_wired_family_routes_to_its_adapter_kind(string family, string adapterMarker)
    {
        using var environment = new EnvironmentScope(
            (OpenAiApiKeyVariable, "fixture-only-key"),
            (OpenCodeApiKeyVariable, "fixture-only-key"),
            ("GOOGLE_API_KEY", null),
            ("LUNATE_OTEL", null),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", null)
        );
        ProviderPreset preset = ProviderPresets.Find("opencode-go")!;
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        var model = new ModelInfo("beam", "opencode-go", preset.BaseUrl, ContextWindow: 8192, true)
        {
            ApiFamily = family,
        };

        IChatClient client = factory.Create(model);

        // The innermost layer is the SDK's IChatClient adapter; its full type name names which
        // SDK the family selected.
        string leaf = WalkChain(client)[^1].GetType().FullName ?? "";
        Assert.Contains(adapterMarker, leaf, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("google")]
    [InlineData("opencode-go")]
    public void A_google_family_model_routes_to_the_native_client_by_name(string providerId)
    {
        using var environment = new EnvironmentScope(
            ("GOOGLE_API_KEY", "fixture-only-key"),
            ("OPENCODE_API_KEY", "fixture-only-key"),
            ("LUNATE_OTEL", null),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", null)
        );
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        ProviderPreset preset = ProviderPresets.Find(providerId)!;
        var model = new ModelInfo("m", providerId, preset.BaseUrl, ContextWindow: 8192, true)
        {
            ApiFamily = "google",
        };

        IChatClient client = factory.Create(model);

        string leaf = WalkChain(client)[^1].GetType().Name;
        Assert.Contains("GoogleGenAIChatClient", leaf, StringComparison.Ordinal);
    }

    [Fact]
    public void A_multi_family_model_without_an_answer_names_the_provider_the_model_and_the_families()
    {
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        var model = new ModelInfo("beam", "opencode-go", Endpoint: null, ContextWindow: 8192, true)
        {
            ApiFamily = null,
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(model)
        );

        Assert.Contains("'opencode-go'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'beam'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("anthropic-messages", exception.Message, StringComparison.Ordinal);
        Assert.Contains("google", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_model_family_is_refused_naming_the_value()
    {
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        var model = new ModelInfo("m", "openai", Endpoint: null, ContextWindow: 8192, true)
        {
            ApiFamily = "mistery",
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            factory.Create(model)
        );

        Assert.Contains("'mistery'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("anthropic-messages", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_messages_model_on_a_compatible_preset_routes_to_the_family_declared_url()
    {
        using var environment = new EnvironmentScope(
            ("OPENCODE_API_KEY", "fixture-only-key"),
            ("LUNATE_OTEL", null),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", null)
        );
        ProviderPreset preset = ProviderPresets.Find("opencode-go")!;
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        var model = new ModelInfo("beam", "opencode-go", Endpoint: null, ContextWindow: 8192, true)
        {
            ApiFamily = "anthropic-messages",
        };

        IChatClient client = factory.Create(model);

        var metadata = (ChatClientMetadata)client.GetService(typeof(ChatClientMetadata))!;
        // The Anthropic SDK assembles the version path itself (messages), so the base it holds
        // is the family URL stripped of its trailing version segment — the wire the request
        // eventually builds is pinned by the messages-family URL test in OpenCodeGoWireTests.
        Uri messagesFamily = preset.UrlFor(ProviderApiFamily.AnthropicMessages)!;
        Assert.Equal(
            ChatClientFactory.SplitApiVersion(messagesFamily).BaseUrl,
            metadata.ProviderUri
        );
    }

    [Fact]
    public void A_family_url_of_the_preset_counts_as_an_own_endpoint_for_the_credential()
    {
        ProviderPreset preset = ProviderPresets.Find("opencode-go")!;
        var model = new ModelInfo(
            "m",
            "opencode-go",
            preset.UrlFor(ProviderApiFamily.AnthropicMessages),
            ContextWindow: 8192,
            true
        )
        {
            ApiFamily = "anthropic-messages",
            AuthRef = "opencode-go",
        };

        string credential = ChatClientFactory.ResolveApiKey(
            model,
            preset,
            namedKeySource: name => name == "opencode-go" ? "stored-key" : null
        );

        Assert.Equal("stored-key", credential);
    }

    [Theory]
    [InlineData("openrouter", "OPENROUTER_API_KEY")]
    [InlineData("opencode", "OPENCODE_API_KEY")]
    public void A_model_without_a_family_on_a_systemone_preset_resolves_the_single_chat_family(
        string providerId,
        string keyVariable
    )
    {
        using var environment = new EnvironmentScope(
            (keyVariable, "fixture-only-key"),
            ("LUNATE_OTEL", null),
            ("OTEL_EXPORTER_OTLP_ENDPOINT", null)
        );
        var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
        var model = new ModelInfo("foo", providerId, Endpoint: null, ContextWindow: 8192, true)
        {
            ApiFamily = null,
        };

        IChatClient client = factory.Create(model);

        string leaf = WalkChain(client)[^1].GetType().FullName ?? "";
        Assert.Contains("OpenAIChatClient", leaf, StringComparison.Ordinal);
    }

    [Fact]
    public void The_chat_families_view_excludes_only_the_decision_family()
    {
        ProviderPreset openrouter = ProviderPresets.Find("openrouter")!;
        ProviderPreset opencode = ProviderPresets.Find("opencode")!;
        ProviderPreset go = ProviderPresets.Find("opencode-go")!;

        Assert.Equal([ProviderApiFamily.OpenAiChat], openrouter.ChatFamilies);
        Assert.Equal([ProviderApiFamily.OpenAiChat], opencode.ChatFamilies);
        Assert.Equal(
            [
                ProviderApiFamily.OpenAiChat,
                ProviderApiFamily.OpenAiResponses,
                ProviderApiFamily.AnthropicMessages,
                ProviderApiFamily.Google,
            ],
            go.ChatFamilies
        );
        Assert.Equal(
            [
                ProviderApiFamily.OpenAiChat,
                ProviderApiFamily.OpenAiResponses,
                ProviderApiFamily.AnthropicMessages,
                ProviderApiFamily.Google,
                ProviderApiFamily.SystemOne,
            ],
            go.Families
        );
        Assert.Contains(ProviderApiFamily.SystemOne, openrouter.Families);
    }

    [Fact]
    public void A_multi_chat_family_ambiguity_names_models_json()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            ProviderFamilyResolver.Resolve(
                declaredFamily: null,
                [ProviderApiFamily.OpenAiChat, ProviderApiFamily.Google],
                "gateway",
                "m"
            )
        );

        Assert.Contains("chat API family", exception.Message, StringComparison.Ordinal);
        Assert.Contains("models.json", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("systemone", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_systemone_family_in_the_offered_set_never_counts_as_a_chat_answer()
    {
        ProviderApiFamily family = ProviderFamilyResolver.Resolve(
            declaredFamily: null,
            [ProviderApiFamily.OpenAiChat, ProviderApiFamily.SystemOne],
            "openrouter",
            "openrouter/foo"
        );

        Assert.Equal(ProviderApiFamily.OpenAiChat, family);
    }

    private static IChatClient[] WalkChain(IChatClient client)
    {
        var chain = new List<IChatClient>();
        for (IChatClient? current = client; current is not null; current = InnerClientOf(current))
        {
            chain.Add(current);
        }

        return [.. chain];
    }

    private static IChatClient? InnerClientOf(IChatClient client)
    {
        for (Type? type = client.GetType(); type is not null; type = type.BaseType)
        {
            PropertyInfo? property = type.GetProperty(
                "InnerClient",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );
            if (property is not null)
            {
                return (IChatClient?)property.GetValue(client);
            }
        }

        return null;
    }
}
