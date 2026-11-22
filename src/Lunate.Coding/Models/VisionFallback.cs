using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The configured vision profile: the existing <c>fast</c> usage when its resolved model declares
/// image input. There is no separate settings key — #253/#357 forbid a new usage — so binding
/// <c>fast</c> to a vision model is how a user turns the fallback on.
/// </summary>
internal static class VisionFallback
{
    internal const string Usage = "fast";

    /// <summary>The resolved fast profile when its model can see images; null otherwise.</summary>
    internal static ResolvedModelProfile? ResolveProfile(
        AgentSettings settings,
        IReadOnlyList<ProviderEntry> providers,
        ModelCatalog catalog
    )
    {
        if (
            !settings.Usages.TryGetValue(Usage, out string? name) || string.IsNullOrWhiteSpace(name)
        )
        {
            return null;
        }

        ResolvedModelProfile profile = ModelSelectionResolver.ResolveChatModel(
            name,
            settings,
            providers,
            catalog
        );
        DecisionUsageModels.RequireChatFamily(profile, usage: Usage);
        return SupportsImageInput(profile.Model) ? profile : null;
    }

    /// <summary>A configured vision profile for the early image gate, or null when there is none.</summary>
    internal static ResolvedModelProfile? TryResolveProfile(
        AgentSettings settings,
        IReadOnlyList<ProviderEntry> providers,
        ModelCatalog catalog
    )
    {
        try
        {
            return ResolveProfile(settings, providers, catalog);
        }
        catch (Exception exception)
            when (exception
                    is UnknownModelException
                        or InvalidDataException
                        or InvalidOperationException
                        or ArgumentException
            )
        {
            return null;
        }
    }

    /// <summary>Whether the model's resolved facts declare image input; unknown is not a yes.</summary>
    internal static bool SupportsImageInput(ModelInfo model) =>
        model.Capabilities?.InputModalities is { } modalities
            ? modalities.Contains("image")
            : model.SupportsVision;

    /// <summary>The usage client for a resolved vision profile, built like every other usage client.</summary>
    internal static UsageModelClient CreateClient(
        ResolvedModelProfile profile,
        AuthStore auth,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context = null
    ) =>
        new(
            new DeferredChatClient(() =>
                DecisionUsageModels.CreateUsageClient(profile, auth, environment, factory, context)
            ),
            profile.Profile.Name,
            profile.Provider.Name,
            profile.Model
        )
        {
            Description = profile.Profile.Description,
        };
}
