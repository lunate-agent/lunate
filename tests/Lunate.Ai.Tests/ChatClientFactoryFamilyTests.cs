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
        Assert.Equal(
            preset.UrlFor(ProviderApiFamily.AnthropicMessages),
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
