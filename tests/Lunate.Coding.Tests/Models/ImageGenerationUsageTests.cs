using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>image</c> usage's declared requirement: a known output-modality set without image output is
/// refused, an unknown set stays accepted, only the openai-chat wire is served, an unusable profile
/// warns once naming the settings key, and an unset usage is a silent off state.
/// </summary>
public sealed class ImageGenerationUsageTests
{
    [Fact]
    public void A_known_output_set_without_image_is_refused()
    {
        ResolvedModelProfile profile = Profile(
            new ModelCapabilityDescriptor(
                outputModalities: new HashSet<string>(["text"], StringComparer.Ordinal)
            )
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ImageGenerationUsage.RequireImageOutput(profile)
        );

        Assert.Contains("gpt-image-1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("image output", exception.Message, StringComparison.Ordinal);
        Assert.Contains("usages.image", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_known_output_set_with_image_is_accepted()
    {
        ResolvedModelProfile profile = Profile(
            new ModelCapabilityDescriptor(
                outputModalities: new HashSet<string>(["image"], StringComparer.Ordinal)
            )
        );

        ImageGenerationUsage.RequireImageOutput(profile);
    }

    [Fact]
    public void An_unknown_capability_set_is_accepted()
    {
        ResolvedModelProfile profile = Profile(capabilities: null);

        ImageGenerationUsage.RequireImageOutput(profile);
    }

    [Fact]
    public void A_family_without_an_image_wire_is_refused_by_name()
    {
        ResolvedModelProfile profile = Profile(
            capabilities: null,
            apiFamily: ProviderApiFamilies.AnthropicMessages
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ImageGenerationUsage.RequireImageOutput(profile)
        );

        Assert.Contains(
            ProviderApiFamilies.AnthropicMessages,
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            ProviderApiFamilies.OpenAiChat,
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_unset_usage_is_silent_and_resolves_nothing()
    {
        using var temp = new TempDirectory();
        AgentSettings settings = SettingsStore.Resolve(temp.File("missing.json"), _ => null);
        List<string> warnings = [];

        ImageGenerationBinding? binding = Resolve(temp, settings, warnings);

        Assert.Null(binding);
        Assert.Empty(warnings);
    }

    [Fact]
    public void An_unusable_profile_warns_once_naming_the_settings_key()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "usages": { "image": "missing-profile" }
            }
            """
        );
        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);
        List<string> warnings = [];

        ImageGenerationBinding? binding = Resolve(temp, settings, warnings);

        Assert.Null(binding);
        string warning = Assert.Single(warnings);
        Assert.Contains("usages.image", warning, StringComparison.Ordinal);
        Assert.Contains("missing-profile", warning, StringComparison.Ordinal);
        Assert.Contains("generate_image", warning, StringComparison.Ordinal);
    }

    private static ImageGenerationBinding? Resolve(
        TempDirectory temp,
        AgentSettings settings,
        List<string> warnings
    ) =>
        ImageGenerationUsage.TryResolve(
            settings,
            [],
            ModelCatalog.Load(temp.File("missing-models.json")),
            AuthStore.Load(temp.File("missing-auth.json"), _ => null),
            _ => null,
            new ChatClientFactory(
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            ),
            context: null,
            warnings.Add
        );

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
            "gpt-image-1",
            "openai",
            new Uri("https://api.openai.com/v1"),
            ContextWindow: 0,
            SupportsTools: false
        )
        {
            ApiFamily = apiFamily,
            Capabilities = capabilities,
        };
        return new ResolvedModelProfile(
            new ModelProfile("image-profile", "fake-provider", model.Id),
            entry,
            model,
            ContextWindow: 0,
            Capabilities: capabilities ?? new ModelCapabilityDescriptor()
        );
    }
}
