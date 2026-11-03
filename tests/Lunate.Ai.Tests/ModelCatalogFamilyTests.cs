namespace Lunate.Ai.Tests;

/// <summary>The catalog's <c>apiFamily</c> annotation: built-ins, user files and refusals.</summary>
public sealed class ModelCatalogFamilyTests
{
    [Fact]
    public void Built_in_entries_for_multi_family_presets_are_annotated_with_the_default_family()
    {
        ModelCatalog catalog = ModelCatalog.LoadLayered(managedFilePath: null, userFilePath: null);

        foreach (ModelInfo model in catalog.Models)
        {
            if (model.ApiFamily == "systemone")
            {
                // The decision models: catalog-declared native entries, not generated chat ones.
                Assert.True(
                    model.Id is "openrouter/typesafe/jev-1.14" or "opencode/jev-1.13",
                    $"Unexpected systemone catalog entry: {model.Id}"
                );
                Assert.Equal("native", model.ApiType);
            }
            else if (model.Provider is "opencode-go" or "google" or "openrouter" or "opencode")
            {
                Assert.Equal(ProviderApiFamilies.OpenAiChat, model.ApiFamily);
            }
            else
            {
                Assert.Null(model.ApiFamily);
            }
        }
    }

    [Fact]
    public void A_user_catalog_file_annotates_the_model_family()
    {
        using var temp = new TempDirectory();
        string path = temp.File("models.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "samples/vendor/m", "provider": "samples", "contextWindow": 8192,
                  "supportsTools": true, "apiFamily": "anthropic-messages" }
              ]
            }
            """
        );

        ModelCatalog catalog = ModelCatalog.Load(path);

        Assert.Equal("anthropic-messages", catalog.Find("samples/vendor/m")!.ApiFamily);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("responses")]
    public void An_unknown_api_family_is_refused_by_name(string family)
    {
        using var temp = new TempDirectory();
        string path = temp.File("models.json");
        File.WriteAllText(
            path,
            $$"""{ "schemaVersion": 2, "models": [ { "id": "samples/vendor/m", "provider": "samples", "contextWindow": 8192, "supportsTools": true, "apiFamily": "{{family}}" } ] }"""
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ModelCatalog.Load(path)
        );

        Assert.Contains(family, exception.Message, StringComparison.Ordinal);
        Assert.Contains("apiFamily", exception.Message, StringComparison.Ordinal);
        Assert.Contains("anthropic-messages", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_schema_1_override_keeps_the_lower_entry_api_family()
    {
        using var temp = new TempDirectory();
        string lower = temp.File("lower.json");
        string upper = temp.File("upper.json");
        File.WriteAllText(
            lower,
            """{ "schemaVersion": 2, "models": [ { "id": "samples/vendor/m", "provider": "samples", "contextWindow": 8192, "supportsTools": true, "apiFamily": "google" } ] }"""
        );
        File.WriteAllText(
            upper,
            """{ "schemaVersion": 1, "models": [ { "id": "samples/vendor/m", "provider": "samples", "contextWindow": 4096 } ] }"""
        );

        ModelCatalog catalog = ModelCatalog.LoadLayered(lower, upper);

        ModelInfo merged = catalog.Find("samples/vendor/m")!;
        Assert.Equal(4096, merged.ContextWindow);
        Assert.Equal("google", merged.ApiFamily);
    }
}
