using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The resolved embeddings consumer: the client a search call uses, and the profile, provider and
/// model its usage is attributed to. Built once per session composition when
/// <c>semantic_search</c> is enabled, never at startup for a run that does not use it.
/// </summary>
internal sealed record EmbeddingsBinding(
    IEmbeddingsClient Client,
    string ProfileName,
    string ProviderName,
    string ProviderKind,
    string ModelId
);

/// <summary>
/// The <c>embeddings</c> usage: the profile whose model declares the embeddings API, resolved the
/// way every usage is (ADR-0027's usage-role rule). The requirement is checked only when the
/// catalog knows the model's API set — an unknown set stays accepted, because an unknown
/// capability is not a known failure — and only the <c>openai-chat</c> family has an embeddings
/// wire in this build.
/// </summary>
internal static class EmbeddingsUsage
{
    internal const string Usage = "embeddings";

    /// <summary>The capability value the usage requires.</summary>
    internal const string RequiredApi = "embeddings";

    /// <summary>
    /// The binding for a run that enabled <c>semantic_search</c>, or null with one warning naming
    /// the settings key. A resolution failure is a warning, not a crash: the tool is optional and
    /// an unprimeable binding only means it is not offered.
    /// </summary>
    internal static EmbeddingsBinding? TryResolve(
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
            warn(
                "semantic_search is enabled in 'tools.extra' but no 'usages.embeddings' profile is "
                    + "set; bind 'usages.embeddings' to a profile whose model declares the "
                    + "embeddings API to offer the tool."
            );
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
            RequireEmbeddingsCapability(profile);
            return new EmbeddingsBinding(
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
                $"semantic_search is enabled but the embeddings profile '{profileName}' is "
                    + $"unavailable, so the tool is not offered: {exception.Message}"
            );
            return null;
        }
    }

    /// <summary>
    /// Refuses a profile that cannot serve the embeddings usage: a known API set without
    /// <c>embeddings</c>, or a family this build has no embeddings wire for.
    /// </summary>
    internal static void RequireEmbeddingsCapability(ResolvedModelProfile profile)
    {
        if (
            profile.Capabilities.Apis is { } apis
            && !apis.Contains(RequiredApi, StringComparer.Ordinal)
        )
        {
            throw new InvalidDataException(
                $"Profile '{profile.Profile.Name}' resolves model '{profile.Model.Id}', whose known "
                    + "capabilities do not include the 'embeddings' API; bind 'usages.embeddings' "
                    + "to an embedding model instead."
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
                    + $"family '{profile.Model.ApiFamily ?? "unknown"}'; this build sends "
                    + $"embeddings on '{ProviderApiFamilies.OpenAiChat}' only. Point "
                    + "'usages.embeddings' at an openai-chat embedding model."
            );
        }
    }

    private static IEmbeddingsClient CreateClient(
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
                $"The embeddings profile '{profile.Profile.Name}' needs the provider client "
                    + "factory; the configured factory cannot build the provider's HTTP client."
            );
        }

        (string? key, ModelInfo wireModel) = DecisionUsageModels.ResolveCredentialReference(
            profile,
            auth,
            environment
        );
        return new ProviderEmbeddingsClient(
            providerFactory.CreateEmbeddingGenerator(wireModel, key, context),
            profile.Model.Id
        );
    }
}
