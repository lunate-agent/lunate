using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

public sealed class VisionFallbackTests
{
    [Fact]
    public void The_vision_usage_is_the_vision_profile_only_when_its_model_sees_images()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "vision", "provider": "openai", "contextWindow": 1000, "supportsTools": true, "supportsVision": true }""",
            """{ "id": "text", "provider": "openai", "contextWindow": 1000, "supportsTools": true }"""
        );
        ProviderEntry provider = Provider();

        ResolvedModelProfile? vision = VisionFallback.ResolveProfile(
            Settings("vision", provider.Name),
            [provider],
            catalog
        );
        ResolvedModelProfile? text = VisionFallback.ResolveProfile(
            Settings("text", provider.Name),
            [provider],
            catalog
        );

        Assert.Equal("vision", vision?.Model.Id);
        Assert.Null(text);
    }

    [Fact]
    public void The_fast_usage_is_not_a_vision_profile()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "vision", "provider": "openai", "contextWindow": 1000, "supportsTools": true, "supportsVision": true }"""
        );
        ProviderEntry provider = Provider();
        var settings = new AgentSettings
        {
            Profiles = new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
            {
                ["fast"] = new("fast", provider.Name, "vision"),
            },
            Usages = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["chat"] = "fast",
                ["fast"] = "fast",
            },
        };

        Assert.Null(VisionFallback.ResolveProfile(settings, [provider], catalog));
    }

    [Fact]
    public void No_vision_usage_means_no_vision_profile()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """{ "id": "vision", "provider": "openai", "contextWindow": 1000, "supportsTools": true, "supportsVision": true }"""
        );
        ProviderEntry provider = Provider();
        var settings = new AgentSettings
        {
            Profiles = new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
            {
                ["vision"] = new("vision", provider.Name, "vision"),
            },
        };

        Assert.Null(VisionFallback.ResolveProfile(settings, [provider], catalog));
    }

    private static AgentSettings Settings(string model, string provider) =>
        new()
        {
            Profiles = new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
            {
                ["vision"] = new("vision", provider, model),
            },
            Usages = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["chat"] = "vision",
                ["vision"] = "vision",
            },
        };

    private static ModelCatalog Catalog(TempDirectory temp, params string[] models)
    {
        string path = temp.File("models.json");
        JsonElement[] entries = models
            .Select(model =>
            {
                using JsonDocument document = JsonDocument.Parse(model);
                return document.RootElement.Clone();
            })
            .ToArray();
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(new { schemaVersion = 2, models = entries })
        );
        return ModelCatalog.Load(path);
    }

    private static ProviderEntry Provider()
    {
        bool valid = ProviderEntry.TryCreate(
            "provider",
            "openai",
            new Uri("https://provider.example.test/v1"),
            "auth:provider",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );

        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }
}
