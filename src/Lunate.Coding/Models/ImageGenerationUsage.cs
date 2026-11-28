using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The resolved image-generation consumer: the client a generation call uses, and the profile,
/// provider and model its usage is attributed to. Built once per session composition when
/// <c>usages.image</c> resolves, never at startup for a run without one.
/// </summary>
internal sealed record ImageGenerationBinding(
    IImageGenerationClient Client,
    string ProfileName,
    string ProviderName,
    string ProviderKind,
    string ModelId
);

/// <summary>
/// The <c>image</c> usage: the profile whose model declares image output, resolved the way every
/// usage is (ADR-0027's usage-role rule). The requirement is checked only when the catalog knows the
/// model's output modalities — an unknown set stays accepted, because an unknown capability is not a
/// known failure — and only the <c>openai-chat</c> family has an image wire in this build.
/// </summary>
internal static class ImageGenerationUsage
{
    internal const string Usage = "image";

    /// <summary>The output modality the usage requires.</summary>
    internal const string RequiredOutputModality = "image";

    /// <summary>
    /// The binding for a run with a configured image profile, or null with one warning naming the
    /// profile and the settings key. An unset usage is the normal off state and warns nothing.
    /// </summary>
    internal static ImageGenerationBinding? TryResolve(
        AgentSettings settings,
        IReadOnlyList<ProviderEntry> providers,
        ModelCatalog catalog,
        AuthStore auth,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context,
        Action<string> warn
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (
            !settings.Usages.TryGetValue(Usage, out string? profileName)
            || string.IsNullOrWhiteSpace(profileName)
        )
        {
            return null;
        }

        try
        {
            ResolvedModelProfile profile = ModelSelectionResolver.ResolveChatModel(
                profileName,
                settings,
                providers,
                catalog
            );
            RequireImageOutput(profile);
            return new ImageGenerationBinding(
                CreateClient(profile, auth, environment, factory, context),
                profile.Profile.Name,
                profile.Provider.Name,
                profile.Provider.Kind,
                profile.Model.Id
            );
        }
        catch (Exception exception)
            when (exception
                    is UnknownModelException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            warn(
                $"'usages.image' names profile '{profileName}', which is unavailable, so "
                    + $"generate_image is not offered: {exception.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Refuses a profile that cannot serve the image usage: a known output-modality set without
    /// <c>image</c>, or a family this build has no image wire for.
    /// </summary>
    internal static void RequireImageOutput(ResolvedModelProfile profile)
    {
        if (
            profile.Capabilities.OutputModalities is { } modalities
            && !modalities.Contains(RequiredOutputModality, StringComparer.Ordinal)
        )
        {
            throw new InvalidDataException(
                $"Profile '{profile.Profile.Name}' resolves model '{profile.Model.Id}', whose known "
                    + "capabilities do not include image output; bind 'usages.image' to an "
                    + "image-generation model instead."
            );
        }

        if (
            !string.Equals(
                profile.Model.ApiFamily,
                ProviderApiFamilies.OpenAiChat,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidDataException(
                $"Profile '{profile.Profile.Name}' resolves model '{profile.Model.Id}' to API "
                    + $"family '{profile.Model.ApiFamily ?? "unknown"}'; this build sends image "
                    + $"generation on '{ProviderApiFamilies.OpenAiChat}' only. Point "
                    + "'usages.image' at an openai-chat image model."
            );
        }
    }

    private static IImageGenerationClient CreateClient(
        ResolvedModelProfile profile,
        AuthStore auth,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context
    )
    {
        if (factory is not ChatClientFactory providerFactory)
        {
            throw new InvalidOperationException(
                $"The image profile '{profile.Profile.Name}' needs the provider client factory; "
                    + "the configured factory cannot build the provider's HTTP client."
            );
        }

        (string? key, ModelInfo wireModel) = DecisionUsageModels.ResolveCredentialReference(
            profile,
            auth,
            environment
        );
        return new ProviderImageGenerationClient(
            providerFactory.CreateImageGenerationWire(wireModel, key, context)
        );
    }
}
