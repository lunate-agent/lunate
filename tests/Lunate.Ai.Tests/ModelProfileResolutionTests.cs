using System.Text.Json;

namespace Lunate.Ai.Tests;

public sealed class ModelProfileResolutionTests
{
    [Fact]
    public void Decision_profile_accepts_a_chat_api_model_with_adapter_label()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "chat-only-model", "provider": "openai", "contextWindow": 128000,
              "supportsTools": true, "apiType": "adapter",
              "capabilities": { "apis": ["chat"], "features": ["structured-output"] } }
            """
        );
        var decision = new ModelProfile("decision", "work-openai", "chat-only-model");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            decision,
            [Provider("work-openai", "https://work.example.test/v1")],
            catalog
        );

        Assert.Equal("decision", resolved.Profile.Name);
        Assert.Equal("adapter", resolved.Model.ApiType);
        Assert.Contains("chat", resolved.Capabilities.Apis!);
    }

    [Fact]
    public void One_openrouter_provider_entry_resolves_chat_and_decision_profiles()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "openrouter/vendor-a/chat-model", "provider": "openrouter",
              "contextWindow": 128000, "supportsTools": true, "apiType": "adapter" }
            """
        );
        catalog = catalog.WithAdditionalModels([
            new ModelInfo(
                "openrouter/vendor-b/decision-model",
                "openrouter",
                Endpoint: null,
                ContextWindow: 128000,
                SupportsTools: true
            )
            {
                ApiType = "native",
            },
        ]);
        ProviderEntry provider = Provider(
            "gateway",
            "https://openrouter.example.test/api/v1",
            "openrouter"
        );
        var chat = new ModelProfile("chat", "gateway", "openrouter/vendor-a/chat-model");
        var decision = new ModelProfile(
            "decision",
            "gateway",
            "openrouter/vendor-b/decision-model"
        );

        ResolvedModelProfile resolvedChat = ModelProfileResolver.Resolve(chat, [provider], catalog);
        ResolvedModelProfile resolvedDecision = ModelProfileResolver.Resolve(
            decision,
            [provider],
            catalog
        );

        Assert.Same(provider, resolvedChat.Provider);
        Assert.Same(provider, resolvedDecision.Provider);
        Assert.Equal("adapter", resolvedChat.Model.ApiType);
        Assert.Equal("native", resolvedDecision.Model.ApiType);
    }

    [Fact]
    public void A_profile_on_a_systemone_capable_entry_resolves_the_single_chat_family()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "openrouter/vendor-a/chat-model", "provider": "openrouter",
              "contextWindow": 128000, "supportsTools": true }
            """
        );
        var familyUrls = new Dictionary<ProviderApiFamily, Uri>
        {
            [ProviderApiFamily.OpenAiChat] = new("https://openrouter.ai/api/v1"),
            [ProviderApiFamily.SystemOne] = new("https://openrouter.ai/api/v1/systemone"),
        };
        bool valid = ProviderEntry.TryCreate(
            "gateway",
            "openrouter",
            baseUrl: new Uri("https://openrouter.ai/api/v1"),
            "auth:gateway",
            headers: null,
            region: null,
            familyUrls,
            modelsUrl: null,
            out ProviderEntry? provider,
            out string? error
        );
        Assert.True(valid, error);

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            new ModelProfile("chat", "gateway", "openrouter/vendor-a/chat-model"),
            [provider!],
            catalog
        );

        Assert.Equal("openai-chat", resolved.Model.ApiFamily);
        Assert.Equal(familyUrls[ProviderApiFamily.OpenAiChat], resolved.Model.Endpoint);
    }

    [Fact]
    public void Layers_capabilities_from_catalog_through_discovery_to_profile()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "layered-model", "provider": "openai", "contextWindow": 128000,
              "supportsTools": true,
              "capabilities": { "apis": ["chat"], "inputModalities": ["text"],
                "outputModalities": ["text"], "features": ["tools"] } }
            """
        );
        var profile = new ModelProfile(
            "layered-profile",
            "work-openai",
            "layered-model",
            CapabilitiesOverride: new ModelCapabilityDescriptor(
                apis: new HashSet<string>(["decision"], StringComparer.Ordinal),
                features: new HashSet<string>(["structured-output"], StringComparer.Ordinal)
            )
        );
        var discovered = new ModelCapabilityDescriptor(
            inputModalities: new HashSet<string>(["image"], StringComparer.Ordinal),
            outputModalities: new HashSet<string>(["text", "image"], StringComparer.Ordinal)
        );

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            profile,
            [Provider("work-openai", "https://work.example.test/v1")],
            catalog,
            discovered
        );

        Assert.Contains("decision", resolved.Capabilities!.Apis!);
        Assert.Contains("image", resolved.Capabilities.InputModalities!);
        Assert.Contains("image", resolved.Capabilities.OutputModalities!);
        Assert.Contains("structured-output", resolved.Capabilities.Features!);
        Assert.Contains("structured-output", resolved.Model.Capabilities!.Features!);
    }

    [Fact]
    public void Resolves_profile_provider_model_and_validated_options()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "test-model", "provider": "openai", "contextWindow": 128000,
              "supportsTools": true, "reasoning": "effort", "maxOutput": 8192,
              "supportsTemperature": true }
            """
        );
        ProviderEntry provider = Provider("work-openai", "https://work.example.test/v1");
        var profile = new ModelProfile(
            "chat-default",
            "work-openai",
            "test-model",
            ReasoningLevel: "medium",
            MaxOutput: 4096,
            Temperature: 0.4m,
            ContextWindow: 64_000
        );

        ResolvedModelProfile result = ModelProfileResolver.Resolve(profile, [provider], catalog);

        Assert.Equal("chat-default", result.Profile.Name);
        Assert.Equal(provider, result.Provider);
        Assert.Equal("test-model", result.Model.Id);
        Assert.Equal(64_000, result.ContextWindow);
        Assert.Equal(4096, result.Profile.MaxOutput);
        Assert.Equal(0.4m, result.Profile.Temperature);
    }

    [Fact]
    public void Accepts_a_supported_reasoning_budget_within_the_model_output_limit()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "budget-model", "provider": "openai", "contextWindow": 128000,
              "supportsTools": true, "reasoning": "budget", "maxOutput": 8192 }
            """
        );
        var profile = new ModelProfile(
            "budget-profile",
            "work-openai",
            "budget-model",
            ReasoningBudget: 4096
        );

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            profile,
            [Provider("work-openai", "https://work.example.test/v1")],
            catalog
        );

        Assert.Equal(4096, resolved.Profile.ReasoningBudget);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9000)]
    public void Refuses_a_reasoning_budget_outside_model_output_limit(int budget)
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "budget-model", "provider": "openai", "contextWindow": 128000,
              "supportsTools": true, "reasoning": "budget", "maxOutput": 8192 }
            """
        );
        var profile = new ModelProfile(
            "budget-profile",
            "work-openai",
            "budget-model",
            ReasoningBudget: budget
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(
                profile,
                [Provider("work-openai", "https://work.example.test/v1")],
                catalog
            )
        );

        Assert.Contains("reasoningBudget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("budget-profile", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("reasoningLevel")]
    [InlineData("reasoningBudget")]
    [InlineData("maxOutput")]
    [InlineData("temperature")]
    [InlineData("contextWindow")]
    public void Unsupported_profile_option_names_the_field_and_profile(string field)
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "limited-model", "provider": "openai", "contextWindow": 128000,
              "supportsTools": true, "maxOutput": 4096 }
            """
        );
        ProviderEntry provider = Provider("work-openai", "https://work.example.test/v1");
        ModelProfile profile = UnsupportedProfile(field);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(profile, [provider], catalog)
        );

        Assert.Contains(field, exception.Message, StringComparison.Ordinal);
        Assert.Contains(profile.Name, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("request", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ModelProfile UnsupportedProfile(string field)
    {
        var profile = new ModelProfile("unsupported-profile", "work-openai", "limited-model");
        return field switch
        {
            "reasoningLevel" => profile with { ReasoningLevel = "high" },
            "reasoningBudget" => profile with { ReasoningBudget = 2048 },
            "maxOutput" => profile with { MaxOutput = 8192 },
            "temperature" => profile with { Temperature = 0.4m },
            "contextWindow" => profile with { ContextWindow = 256_000 },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
    }

    [Fact]
    public void A_profile_resolves_a_bare_id_its_own_provider_entry_owns()
    {
        // The setup wizard writes a custom endpoint's profile with the discovery listing's bare
        // wire id, while the shipped catalog holds the same bare id under several providers. The
        // profile's own provider answers it, so the configuration the walk wrote loads (#675).
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "local/kimi-k2.7-code", "provider": "openai", "wireId": "kimi-k2.7-code",
              "contextWindow": 262144, "supportsTools": true }
            """
        );
        var profile = new ModelProfile("chat-main", "local", "kimi-k2.7-code");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            profile,
            [Provider("local", "https://local.example.test/v1")],
            catalog
        );

        Assert.Equal("local/kimi-k2.7-code", resolved.Model.Id);
        Assert.Equal("kimi-k2.7-code", resolved.Model.WireId);
        Assert.Equal("https://local.example.test/v1", resolved.Model.Endpoint?.ToString());
    }

    [Fact]
    public void A_profile_resolves_a_bare_id_its_preset_owns()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = ModelCatalog.Load(temp.File("no-user-catalog.json"));
        var profile = new ModelProfile("chat-main", "moonshotai", "kimi-k2.7-code");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            profile,
            [Provider("moonshotai", "https://api.moonshot.example/v1", "moonshotai")],
            catalog
        );

        Assert.Equal("moonshotai/kimi-k2.7-code", resolved.Model.Id);
    }

    [Fact]
    public void A_bare_id_no_provider_entry_owns_still_names_its_candidates()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = ModelCatalog.Load(temp.File("no-user-catalog.json"));
        var profile = new ModelProfile("chat-main", "local", "kimi-k2.7-code");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(
                profile,
                [Provider("local", "https://local.example.test/v1")],
                catalog
            )
        );

        Assert.Contains("model", exception.Message, StringComparison.Ordinal);
        Assert.Contains("moonshotai/kimi-k2.7-code", exception.Message, StringComparison.Ordinal);
        Assert.Contains("opencode/kimi-k2.7-code", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_systemone_profile_with_an_exact_version_resolves()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "openrouter/typesafe/jev-1.14", "provider": "openrouter",
              "wireId": "typesafe/jev-1.14", "contextWindow": 65536,
              "supportsTools": false, "supportsVision": true,
              "apiType": "native", "apiFamily": "systemone" }
            """
        );
        var profile = new ModelProfile("decision", "gateway", "openrouter/typesafe/jev-1.14");

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            profile,
            [Provider("gateway", "https://openrouter.ai/api/v1", "openrouter")],
            catalog
        );

        Assert.Equal("systemone", resolved.Model.ApiFamily);
        Assert.Equal("native", resolved.Model.ApiType);
    }

    [Fact]
    public void A_systemone_alias_is_refused_with_the_exact_version_the_catalog_records()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "openrouter/~typesafe/jev-1.14", "provider": "openrouter",
              "wireId": "~typesafe/jev-1.14", "contextWindow": 65536,
              "supportsTools": false, "supportsVision": true,
              "apiType": "native", "apiFamily": "systemone" }
            """
        );
        catalog = catalog.WithAdditionalModels([
            new ModelInfo(
                "openrouter/typesafe/jev-1.14",
                "openrouter",
                Endpoint: null,
                ContextWindow: 65536,
                SupportsTools: false
            )
            {
                WireId = "typesafe/jev-1.14",
                ApiType = "native",
                ApiFamily = "systemone",
            },
        ]);
        var profile = new ModelProfile("decision", "gateway", "openrouter/~typesafe/jev-1.14");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelProfileResolver.Resolve(
                profile,
                [Provider("gateway", "https://openrouter.ai/api/v1", "openrouter")],
                catalog
            )
        );

        Assert.Contains("~typesafe/jev-1.14", exception.Message, StringComparison.Ordinal);
        Assert.Contains("typesafe/jev-1.14", exception.Message, StringComparison.Ordinal);
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

    private static ProviderEntry Provider(string name, string endpoint, string kind = "openai")
    {
        bool valid = ProviderEntry.TryCreate(
            name,
            kind,
            new Uri(endpoint),
            "auth:openai",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );

        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }
}
