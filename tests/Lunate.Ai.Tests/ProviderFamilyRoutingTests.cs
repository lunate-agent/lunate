using System.Text.Json;

namespace Lunate.Ai.Tests;

/// <summary>
/// The family resolution matrix: profile override, the already-merged discovery/catalog family,
/// the provider's single family, and the ambiguity refusal. Discovery's own metadata reader is a
/// later slice; this consumes an already-resolved family through
/// <see cref="ModelInfo.ApiFamily"/>.
/// </summary>
public sealed class ProviderFamilyRoutingTests
{
    [Fact]
    public void A_profile_override_wins_over_the_model_declared_family()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "go/beam", "provider": "opencode-go", "contextWindow": 8192, "supportsTools": true, "apiFamily": "anthropic-messages" }"""
        );
        ProviderEntry provider = Provider(
            "go",
            "opencode-go",
            baseUrls: new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
                [ProviderApiFamily.OpenAiResponses] = new("https://go.example.test/responses/v1"),
                [ProviderApiFamily.AnthropicMessages] = new("https://go.example.test/anthropic/v1"),
            }
        );
        var profile = new ModelProfile("chat", "go", "go/beam", ApiFamily: "openai-responses");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(profile, [provider], catalog);

        Assert.Equal("openai-responses", resolved.Model.ApiFamily);
        Assert.Equal(new Uri("https://go.example.test/responses/v1"), resolved.Model.Endpoint);
    }

    [Fact]
    public void The_model_declared_family_routes_to_its_endpoint_without_an_override()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "go/beam", "provider": "opencode-go", "contextWindow": 8192, "supportsTools": true, "apiFamily": "anthropic-messages" }"""
        );
        ProviderEntry provider = Provider(
            "go",
            "opencode-go",
            baseUrls: new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
                [ProviderApiFamily.AnthropicMessages] = new("https://go.example.test/anthropic/v1"),
            }
        );
        var profile = new ModelProfile("chat", "go", "go/beam");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(profile, [provider], catalog);

        Assert.Equal("anthropic-messages", resolved.Model.ApiFamily);
        Assert.Equal(new Uri("https://go.example.test/anthropic/v1"), resolved.Model.Endpoint);
    }

    [Fact]
    public void The_built_in_catalog_annotation_answers_an_ambiguous_entry()
    {
        ModelCatalog catalog = ModelCatalog.LoadLayered(managedFilePath: null, userFilePath: null);
        ProviderEntry provider = Provider(
            "go",
            "opencode-go",
            baseUrls: new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
                [ProviderApiFamily.AnthropicMessages] = new("https://go.example.test/anthropic/v1"),
            }
        );
        var profile = new ModelProfile("chat", "go", "opencode-go/beam-free");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(profile, [provider], catalog);

        Assert.Equal("openai-chat", resolved.Model.ApiFamily);
        Assert.Equal(new Uri("https://go.example.test/openai/v1"), resolved.Model.Endpoint);
    }

    [Fact]
    public void A_single_family_provider_answers_without_any_annotation()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "plain", "provider": "openai", "contextWindow": 8192, "supportsTools": true }"""
        );
        ProviderEntry provider = Provider(
            "work",
            "openai",
            endpoint: "https://work.example.test/v1"
        );
        var profile = new ModelProfile("chat", "work", "plain");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(profile, [provider], catalog);

        Assert.Null(resolved.Profile.ApiFamily);
        Assert.Equal("openai-chat", resolved.Model.ApiFamily);
        Assert.Equal(new Uri("https://work.example.test/v1"), resolved.Model.Endpoint);
    }

    [Fact]
    public void An_ambiguity_error_names_the_provider_the_model_and_the_families()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "go/beam", "provider": "opencode-go", "contextWindow": 8192, "supportsTools": true }"""
        );
        ProviderEntry provider = Provider(
            "go",
            "opencode-go",
            baseUrls: new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.OpenAiChat] = new("https://go.example.test/openai/v1"),
                [ProviderApiFamily.OpenAiResponses] = new("https://go.example.test/responses/v1"),
            }
        );
        var profile = new ModelProfile("chat", "go", "go/beam");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(profile, [provider], catalog)
        );

        Assert.Contains("go/beam", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'go'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("openai-chat", exception.Message, StringComparison.Ordinal);
        Assert.Contains("openai-responses", exception.Message, StringComparison.Ordinal);
        Assert.Contains("apiFamily", exception.Message, StringComparison.Ordinal);
        Assert.Contains("chat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_override_family_is_refused_naming_the_value()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "plain", "provider": "openai", "contextWindow": 8192, "supportsTools": true }"""
        );
        ProviderEntry provider = Provider(
            "work",
            "openai",
            endpoint: "https://work.example.test/v1"
        );
        var profile = new ModelProfile("chat", "work", "plain", ApiFamily: "mistery");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(profile, [provider], catalog)
        );

        Assert.Contains("'mistery'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("openai-responses", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_family_the_entry_declares_no_url_for_is_refused_naming_the_family()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "plain", "provider": "openai", "contextWindow": 8192, "supportsTools": true }"""
        );
        ProviderEntry provider = Provider(
            "work",
            "openai",
            endpoint: "https://work.example.test/v1"
        );
        var profile = new ModelProfile("chat", "work", "plain", ApiFamily: "google");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(profile, [provider], catalog)
        );

        Assert.Contains("google", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'work'", exception.Message, StringComparison.Ordinal);
    }

    private static ModelCatalog Catalog(TempDirectory temp, string model)
    {
        string path = temp.File("models.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new { schemaVersion = 2, models = new[] { JsonDocument.Parse(model).RootElement } }
            )
        );
        return ModelCatalog.Load(path);
    }

    private static ProviderEntry Provider(
        string name,
        string kind,
        string? endpoint = null,
        IReadOnlyDictionary<ProviderApiFamily, Uri>? baseUrls = null
    )
    {
        bool created = ProviderEntry.TryCreate(
            name,
            kind,
            endpoint is null ? null : new Uri(endpoint),
            $"auth:{kind}",
            headers: null,
            region: null,
            baseUrls,
            modelsUrl: null,
            out ProviderEntry? entry,
            out string? error
        );

        Assert.True(created, error);
        return Assert.IsType<ProviderEntry>(entry);
    }
}
