namespace Lunate.Ai.Tests;

/// <summary>Per-family URL plumbing on the provider entry: shorthand, map and validation.</summary>
public sealed class ProviderEntryFamilyTests
{
    [Fact]
    public void A_base_url_entry_serves_only_its_kinds_default_family()
    {
        ProviderEntry entry = Create("go", "opencode-go", "https://go.example.test/v1");

        Assert.Equal(ProviderApiFamily.OpenAiChat, entry.DefaultFamily);
        Assert.Equal([ProviderApiFamily.OpenAiChat], entry.Families);
        Assert.Equal(
            new Uri("https://go.example.test/v1"),
            entry.UrlFor(ProviderApiFamily.OpenAiChat)
        );
        Assert.Null(entry.UrlFor(ProviderApiFamily.AnthropicMessages));
    }

    [Fact]
    public void The_anthropic_kind_shorthand_names_the_messages_family()
    {
        ProviderEntry entry = Create("work", "anthropic", "https://work.example.test/v1");

        Assert.Equal(ProviderApiFamily.AnthropicMessages, entry.DefaultFamily);
        Assert.Equal([ProviderApiFamily.AnthropicMessages], entry.Families);
    }

    [Fact]
    public void A_per_family_entry_routes_each_family_to_its_declared_url()
    {
        var familyUrls = new Dictionary<ProviderApiFamily, Uri>
        {
            [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
            [ProviderApiFamily.OpenAiResponses] = new("https://go.example.test/openai/v1"),
            [ProviderApiFamily.AnthropicMessages] = new("https://go.example.test/anthropic/v1"),
        };

        ProviderEntry entry = Create(
            "go",
            "opencode-go",
            "https://go.example.test/openai/v1",
            baseUrls: familyUrls,
            modelsUrl: new Uri("https://go.example.test/v1/models")
        );

        Assert.Equal(
            [
                ProviderApiFamily.OpenAiChat,
                ProviderApiFamily.OpenAiResponses,
                ProviderApiFamily.AnthropicMessages,
            ],
            entry.Families
        );
        Assert.Equal(
            familyUrls[ProviderApiFamily.OpenAiChat],
            entry.UrlFor(ProviderApiFamily.OpenAiChat)
        );
        Assert.Equal(
            familyUrls[ProviderApiFamily.AnthropicMessages],
            entry.UrlFor(ProviderApiFamily.AnthropicMessages)
        );
        Assert.Null(entry.UrlFor(ProviderApiFamily.Google));
        Assert.Equal(new Uri("https://go.example.test/v1/models"), entry.ModelsUrl);
    }

    [Fact]
    public void Base_urls_alone_derive_the_shorthand_from_the_default_family()
    {
        bool created = ProviderEntry.TryCreate(
            "go",
            "opencode-go",
            baseUrl: null,
            "auth:go",
            headers: null,
            region: null,
            new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
            },
            modelsUrl: null,
            out ProviderEntry? entry,
            out string? error
        );

        Assert.True(created, error);
        Assert.Equal(new Uri("https://go.example.test/openai/v1"), entry!.BaseUrl);
    }

    [Fact]
    public void Base_urls_without_the_default_family_and_without_a_shorthand_are_refused()
    {
        bool created = ProviderEntry.TryCreate(
            "go",
            "opencode-go",
            baseUrl: null,
            "auth:go",
            headers: null,
            region: null,
            new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.AnthropicMessages] = new("https://go.example.test/anthropic/v1"),
            },
            modelsUrl: null,
            out _,
            out string? error
        );

        Assert.False(created);
        Assert.Contains("openai-chat", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_family_url_that_is_not_https_or_loopback_is_refused_naming_the_family()
    {
        bool created = ProviderEntry.TryCreate(
            "go",
            "opencode-go",
            new Uri("https://go.example.test/openai/v1"),
            "auth:go",
            headers: null,
            region: null,
            new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
                [ProviderApiFamily.Google] = new("http://evil.example.test/google"),
            },
            modelsUrl: null,
            out _,
            out string? error
        );

        Assert.False(created);
        Assert.Contains("google", error, StringComparison.Ordinal);
        Assert.Contains("https or loopback http", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_models_url_that_is_not_https_or_loopback_is_refused()
    {
        bool created = ProviderEntry.TryCreate(
            "go",
            "opencode-go",
            new Uri("https://go.example.test/openai/v1"),
            "auth:go",
            headers: null,
            region: null,
            baseUrls: null,
            modelsUrl: new Uri("http://evil.example.test/v1/models"),
            out _,
            out string? error
        );

        Assert.False(created);
        Assert.Contains("modelsUrl", error, StringComparison.Ordinal);
        Assert.Contains("https or loopback http", error, StringComparison.Ordinal);
    }

    private static ProviderEntry Create(
        string name,
        string kind,
        string baseUrl,
        IReadOnlyDictionary<ProviderApiFamily, Uri>? baseUrls = null,
        Uri? modelsUrl = null
    )
    {
        bool created = ProviderEntry.TryCreate(
            name,
            kind,
            new Uri(baseUrl),
            $"auth:{kind}",
            headers: null,
            region: null,
            baseUrls,
            modelsUrl,
            out ProviderEntry? entry,
            out string? error
        );

        Assert.True(created, error);
        return Assert.IsType<ProviderEntry>(entry);
    }
}
