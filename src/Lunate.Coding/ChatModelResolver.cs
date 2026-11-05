using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The chat model every coding frontend runs on: the configured <c>usages.chat</c> profile
/// resolved through the provider store, or — only when no chat usage is configured — the legacy
/// top-level <c>model</c>. Interactive, print and ACP enter here, so a wizard-configured install
/// runs the same model, provider entry and thinking in every mode.
/// </summary>
internal static class ChatModelResolver
{
    /// <summary>
    /// The chat usage's resolved profile, or null when no chat usage is configured. Resolution
    /// goes through <see cref="ModelSelectionResolver.ResolveChatModel"/> with the provider-store
    /// entries, so the profile keeps its provider entry, API family, endpoint, headers,
    /// credential placement and reasoning level.
    /// </summary>
    internal static ResolvedModelProfile? ResolveChatProfile(
        AgentSettings settings,
        IReadOnlyList<ProviderEntry> providers,
        ModelCatalog catalog
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(catalog);

        if (
            !settings.Usages.TryGetValue("chat", out string? selection)
            || string.IsNullOrWhiteSpace(selection)
        )
        {
            return null;
        }

        ResolvedModelProfile profile = ModelSelectionResolver.ResolveChatModel(
            selection,
            settings,
            providers,
            catalog
        );
        DecisionUsageModels.RequireChatFamily(profile, usage: "chat");
        return profile;
    }

    /// <summary>
    /// The frontend's chat model: the chat usage's profile when one is configured, otherwise the
    /// legacy top-level <c>model</c> resolved through the catalog. A machine with neither keeps
    /// the existing "no model is configured" refusal.
    /// </summary>
    internal static ChatModelSelection Resolve(
        AgentSettings settings,
        IReadOnlyList<ProviderEntry> providers,
        ModelCatalog catalog
    )
    {
        if (ResolveChatProfile(settings, providers, catalog) is { } profile)
        {
            return new ChatModelSelection(profile, profile.Model);
        }

        return new ChatModelSelection(
            Profile: null,
            HarnessFactory.ResolveModel(settings.Model, catalog)
        );
    }
}

/// <summary>
/// One frontend's chat selection: the resolved profile when <c>usages.chat</c> named one, and the
/// model the harness runs on either way.
/// </summary>
internal sealed record ChatModelSelection(ResolvedModelProfile? Profile, ModelInfo Model)
{
    /// <summary>
    /// The run's chat client. A profile routes through <see cref="DecisionUsageModels.CreateUsageClient"/>,
    /// so its credential reference and provider entry apply exactly as in the interactive session;
    /// the legacy model keeps the plain factory path.
    /// </summary>
    internal IChatClient CreateClient(
        AuthStore auth,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context = null
    ) =>
        Profile is { } profile
            ? DecisionUsageModels.CreateUsageClient(profile, auth, environment, factory, context)
            : factory.Create(Model, context);
}
