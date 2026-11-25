using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>embeddings</c> usage's declared requirement: a known API set without the embeddings API
/// is refused, an unknown set stays accepted, only the openai-chat wire is served, and an unset
/// usage leaves the optional tool off with a notice naming the settings key.
/// </summary>
public sealed class EmbeddingsUsageTests
{
    [Fact]
    public void A_known_api_set_without_embeddings_is_refused()
    {
        ResolvedModelProfile profile = Profile(
            new ModelCapabilityDescriptor(apis: new HashSet<string>(["chat"], StringComparer.Ordinal))
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EmbeddingsUsage.RequireEmbeddingsCapability(profile)
        );

        Assert.Contains("text-embedding-3-small", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'embeddings' API", exception.Message, StringComparison.Ordinal);
        Assert.Contains("usages.embeddings", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_known_api_set_with_embeddings_is_accepted()
    {
        ResolvedModelProfile profile = Profile(
            new ModelCapabilityDescriptor(
                apis: new HashSet<string>(["embeddings"], StringComparer.Ordinal)
            )
        );

        EmbeddingsUsage.RequireEmbeddingsCapability(profile);
    }

    [Fact]
    public void An_unknown_api_set_is_accepted()
    {
        ResolvedModelProfile profile = Profile(capabilities: null);

        EmbeddingsUsage.RequireEmbeddingsCapability(profile);
    }

    [Fact]
    public void A_family_without_an_embeddings_wire_is_refused_by_name()
    {
        ResolvedModelProfile profile = Profile(
            capabilities: null,
            apiFamily: ProviderApiFamilies.AnthropicMessages
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            EmbeddingsUsage.RequireEmbeddingsCapability(profile)
        );

        Assert.Contains(ProviderApiFamilies.AnthropicMessages, exception.Message, StringComparison.Ordinal);
        Assert.Contains(ProviderApiFamilies.OpenAiChat, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unset_usage_is_not_resolved_and_names_the_settings_key()
    {
        using var temp = new TempDirectory();
        AgentSettings settings = SettingsStore.Resolve(temp.File("missing.json"), _ => null);
        List<string> warnings = [];

        EmbeddingsBinding? binding = EmbeddingsUsage.TryResolve(
            settings,
            [],
            ModelCatalog.Load(temp.File("missing-models.json")),
            AuthStore.Load(temp.File("missing-auth.json"), _ => null),
            _ => null,
            new ChatClientFactory(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance),
            context: null,
            warnings.Add
        );

        Assert.Null(binding);
        string warning = Assert.Single(warnings);
        Assert.Contains("usages.embeddings", warning, StringComparison.Ordinal);
        Assert.Contains("semantic_search", warning, StringComparison.Ordinal);
    }

    private static ResolvedModelProfile Profile(
        ModelCapabilityDescriptor? capabilities,
        string apiFamily = ProviderApiFamilies.OpenAiChat
    )
    {
        var entry = new ProviderEntry(
            "fake-provider",
            "openai",
            new Uri("https://api.openai.com/v1"),
            "env:OPENAI_API_KEY"
        );
        ModelInfo model = new(
            "openai/text-embedding-3-small",
            "openai",
            new Uri("https://api.openai.com/v1"),
            ContextWindow: 8191,
            SupportsTools: false
        )
        {
            ApiFamily = apiFamily,
            Capabilities = capabilities,
        };
        return new ResolvedModelProfile(
            new ModelProfile("embedding-profile", "fake-provider", model.Id),
            entry,
            model,
            ContextWindow: 8191,
            Capabilities: capabilities ?? new ModelCapabilityDescriptor()
        );
    }
}
