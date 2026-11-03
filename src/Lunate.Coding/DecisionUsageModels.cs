using Lunate.Agent;
using Lunate.Ai;
using Lunate.Ai.Decisions;
using Lunate.Ai.SystemOne;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The decision-model resolution every coding frontend shares: the configured
/// <c>decisions.profile</c> resolves into the <c>UsageModels["decision"]</c> client the approval
/// gate, the <c>decide</c> tool and the routing consumer read. Print mode and ACP weave it like
/// the interactive session, because the fail-closed approval rule covers every mode. A profile
/// that cannot resolve there — a missing provider, an unavailable credential — degrades to no
/// gate with a visible warning, not a crash.
/// </summary>
internal static class DecisionUsageModels
{
    /// <summary>
    /// A chat usage refuses a <c>systemone</c> profile: the System One family is decision-only,
    /// no chat client is ever built for it, and the refusal names the family and the fix. The
    /// decision usage itself accepts the family — that is its native path.
    /// </summary>
    internal static void RequireChatFamily(ResolvedModelProfile profile, string usage)
    {
        if (
            !string.Equals(
                profile.Model.ApiFamily,
                ProviderApiFamilies.SystemOne,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        throw new InvalidDataException(
            $"Profile '{profile.Profile.Name}' resolves model '{profile.Model.Id}' of the "
                + $"'systemone' API family; System One is decision-only and cannot serve the "
                + $"'{usage}' usage. Choose a chat model for it, or read decisions from it through "
                + "'decisions.profile'."
        );
    }

    /// <summary>
    /// Resolves the configured <c>decisions.profile</c> into the decision usage client, or null
    /// when the layer is unconfigured or the profile cannot resolve. Each resolution failure
    /// warns once through <paramref name="warn"/> (deduplicated by <paramref name="warned"/>);
    /// the sources function loads the credential store and the provider entries, lazily, so a
    /// frontend that never configures the decision layer touches neither.
    /// </summary>
    internal static UsageModelClient? ResolveDecisionModel(
        AgentSettings settings,
        Func<(AuthStore Auth, IReadOnlyList<ProviderEntry> Providers)> sources,
        ModelCatalog catalog,
        Func<string, string?> environment,
        IChatClientFactory factory,
        Action<UserNotice> warn,
        HashSet<string> warned,
        RequestContext? context = null
    )
    {
        if (settings.Decisions.Profile is not { } profile)
        {
            return null;
        }

        try
        {
            (AuthStore auth, IReadOnlyList<ProviderEntry> providers) = sources();
            ResolvedModelProfile resolved = ModelSelectionResolver.ResolveChatModel(
                profile,
                settings,
                providers,
                catalog
            );
            return BuildClient(resolved, auth, environment, factory, context);
        }
        catch (Exception exception)
            when (exception
                    is UnknownModelException
                        or InvalidDataException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or ArgumentException
            )
        {
            if (warned.Add(profile))
            {
                warn(
                    new UserNotice(
                        NoticeLevel.Warning,
                        $"decision profile '{profile}' is unavailable; the decision layer is off: {exception.Message}"
                    )
                );
            }

            return null;
        }
    }

    /// <summary>
    /// Resolves the routing profiles the routing consumer selects between; empty or unresolved
    /// names are a settings-validation error before this point, so a failure here surfaces as a
    /// composition error rather than a degraded gate.
    /// </summary>
    internal static IReadOnlyDictionary<string, UsageModelClient>? ResolveRoutingProfiles(
        AgentSettings settings,
        IReadOnlyList<ProviderEntry> providers,
        AuthStore auth,
        ModelCatalog catalog,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context = null
    )
    {
        if (settings.Decisions.Routing == DecisionMode.Off)
        {
            return null;
        }

        var profiles = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal);
        foreach (string profileName in settings.Decisions.RoutingProfiles)
        {
            ResolvedModelProfile profile = ModelSelectionResolver.ResolveChatModel(
                profileName,
                settings,
                providers,
                catalog
            );
            RequireChatFamily(profile, usage: $"routingProfiles/{profileName}");
            profiles.Add(profileName, BuildClient(profile, auth, environment, factory, context));
        }

        return profiles;
    }

    private static UsageModelClient BuildClient(
        ResolvedModelProfile profile,
        AuthStore auth,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context = null
    ) =>
        new(
            new DeferredChatClient(() =>
                CreateUsageClient(profile, auth, environment, factory, context)
            ),
            profile.Profile.Name,
            profile.Provider.Name,
            profile.Model
        )
        {
            Description = profile.Profile.Description,
        };

    /// <summary>
    /// Materializes a profile's client from its credential reference: an environment or stored key
    /// when it resolves, otherwise a deferred factory call that fails at first use with the
    /// provider's message. A <c>systemone</c> profile materializes the native decision transport
    /// instead — the decision-only chat facade over the System One HTTP client, which the shared
    /// header and credential handlers serve like the chat wires do. Shared by the interactive
    /// session's own usage resolution, print mode and ACP.
    /// </summary>
    internal static IChatClient CreateUsageClient(
        ResolvedModelProfile profile,
        AuthStore auth,
        Func<string, string?> environment,
        IChatClientFactory factory,
        RequestContext? context = null
    )
    {
        if (
            string.Equals(
                profile.Model.ApiFamily,
                ProviderApiFamilies.SystemOne,
                StringComparison.Ordinal
            )
        )
        {
            return CreateSystemOneClient(profile, environment, auth, factory, context);
        }

        string reference = profile.Provider.CredentialReference;
        if (reference.StartsWith("auth:", StringComparison.Ordinal))
        {
            string keyName = reference["auth:".Length..];
            ProviderPreset? preset = ProviderPresets.Find(profile.Provider.Kind);
            string? environmentKey = preset is null
                ? null
                : environment(preset.KeyVariable)
                    ?? (preset.KeyVariableAlias is { } alias ? environment(alias) : null);
            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                return factory.Create(profile.Model, environmentKey, context);
            }

            string? storedKey = auth.TryGet(keyName);
            if (!string.IsNullOrWhiteSpace(storedKey))
            {
                return factory.Create(profile.Model, storedKey, context);
            }

            return factory.Create(profile.Model with { AuthRef = keyName }, context);
        }

        if (reference.StartsWith("env:", StringComparison.Ordinal))
        {
            string variable = reference["env:".Length..];
            string? environmentKey = environment(variable);
            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                return factory.Create(profile.Model, environmentKey, context);
            }

            throw new InvalidOperationException(
                $"The environment credential '{variable}' for provider '{profile.Provider.Name}' is not set. Set {variable} before using this profile."
            );
        }

        throw new InvalidOperationException(
            $"The credential helper '{reference["helper:".Length..]}' for provider '{profile.Provider.Name}' cannot be resolved by this host. Use an auth or env credential reference."
        );
    }

    /// <summary>
    /// The System One client: the provider's HTTP transport (shared headers, credential placement)
    /// under the decision-only facade, so the resolution seam keeps its <c>IChatClient</c> shape
    /// while every chat call on the model still refuses. The preset carries the family's URL, its
    /// image-only headers and the wire model id (the catalog id is never put on the System One
    /// wire).
    /// </summary>
    private static IChatClient CreateSystemOneClient(
        ResolvedModelProfile profile,
        Func<string, string?> environment,
        AuthStore auth,
        IChatClientFactory factory,
        RequestContext? context
    )
    {
        ProviderPreset? preset = ProviderPresets.Find(profile.Provider.Kind);
        if (preset is null)
        {
            throw new InvalidOperationException(
                $"Provider kind '{profile.Provider.Kind}' of profile '{profile.Profile.Name}' names no preset; a System One profile needs the preset's family URL."
            );
        }

        if (profile.Model.Endpoint is not { } endpoint)
        {
            throw new InvalidOperationException(
                $"Profile '{profile.Profile.Name}' resolves no System One endpoint."
            );
        }

        if (factory is not ChatClientFactory concrete)
        {
            throw new InvalidOperationException(
                $"The System One transport for model '{profile.Model.Id}' needs the provider client factory; the configured factory cannot build the provider's HTTP client."
            );
        }

        (string? resolvedKey, ModelInfo wireModel) = ResolveSystemOneCredential(
            profile,
            environment,
            auth
        );
        HttpClient httpClient = concrete.CreateProviderHttpClient(
            wireModel,
            resolvedKey,
            context,
            ProviderApiFamily.SystemOne
        );
        var systemOnePreset = new SystemOnePreset(
            endpoint,
            profile.Provider.HeadersFor(ProviderApiFamily.SystemOne),
            wireModel.WireId ?? wireModel.Id
        );
        return new SystemOneChatFacade(
            profile.Model.Id,
            new SystemOneDecisionClient(httpClient, systemOnePreset)
        );
    }

    /// <summary>
    /// The credential a System One transport sends: the environment key of the provider's
    /// credential reference first, then the auth store under it, then the reference itself as an
    /// auth name the client factory resolves at first use.
    /// </summary>
    private static (string? ResolvedKey, ModelInfo WireModel) ResolveSystemOneCredential(
        ResolvedModelProfile profile,
        Func<string, string?> environment,
        AuthStore auth
    )
    {
        string reference = profile.Provider.CredentialReference;
        ModelInfo wireModel = profile.Model;
        if (reference.StartsWith("auth:", StringComparison.Ordinal))
        {
            string keyName = reference["auth:".Length..];
            ProviderPreset? preset = ProviderPresets.Find(profile.Provider.Kind);
            string? environmentKey = preset is null
                ? null
                : environment(preset.KeyVariable)
                    ?? (preset.KeyVariableAlias is { } alias ? environment(alias) : null);
            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                return (environmentKey, wireModel);
            }

            string? storedKey = auth.TryGet(keyName);
            if (!string.IsNullOrWhiteSpace(storedKey))
            {
                return (storedKey, wireModel);
            }

            return (null, wireModel with { AuthRef = keyName });
        }

        if (reference.StartsWith("env:", StringComparison.Ordinal))
        {
            string variable = reference["env:".Length..];
            string? environmentKey = environment(variable);
            if (string.IsNullOrWhiteSpace(environmentKey))
            {
                throw new InvalidOperationException(
                    $"The environment credential '{variable}' for provider '{profile.Provider.Name}' is not set. Set {variable} before using this profile."
                );
            }

            return (environmentKey, wireModel);
        }

        throw new InvalidOperationException(
            $"The credential helper '{reference["helper:".Length..]}' for provider '{profile.Provider.Name}' cannot be resolved by this host. Use an auth or env credential reference."
        );
    }
}
