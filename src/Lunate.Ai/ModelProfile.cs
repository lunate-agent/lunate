namespace Lunate.Ai;

/// <summary>A named model choice and the optional settings validated against its catalog model.
/// <see cref="ApiFamily"/> is the optional wire-name override (<c>openai-chat</c>,
/// <c>openai-responses</c>, <c>anthropic-messages</c>, <c>google</c>, <c>systemone</c>) that pins
/// which API family the model resolves to; it wins over discovery and the catalog. A
/// <c>systemone</c> profile names the native decision transport and is refused as a chat usage.</summary>
internal sealed record ModelProfile(
    string Name,
    string Provider,
    string Model,
    string? ReasoningLevel = null,
    int? ReasoningBudget = null,
    int? MaxOutput = null,
    decimal? Temperature = null,
    int? ContextWindow = null,
    ModelCapabilityDescriptor? CapabilitiesOverride = null,
    string? ApiFamily = null,
    string? Description = null
);

/// <summary>A profile resolved to one user provider entry and one catalog model.</summary>
internal sealed record ResolvedModelProfile(
    ModelProfile Profile,
    ProviderEntry Provider,
    ModelInfo Model,
    int ContextWindow,
    ModelCapabilityDescriptor Capabilities
);

/// <summary>Resolves and validates a profile before any model client can be constructed.</summary>
internal static class ModelProfileResolver
{
    public static ResolvedModelProfile Resolve(
        ModelProfile profile,
        IReadOnlyList<ProviderEntry> providers,
        ModelCatalog catalog,
        ModelCapabilityDescriptor? discoveredCapabilities = null
    )
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(catalog);

        ProviderEntry? provider = providers.SingleOrDefault(entry =>
            string.Equals(entry.Name, profile.Provider, StringComparison.Ordinal)
        );
        if (provider is null)
        {
            throw Invalid(profile, "provider", "does not name a configured provider");
        }

        ModelInfo? model;
        try
        {
            model = catalog.Find(profile.Model);
        }
        catch (InvalidOperationException exception)
        {
            throw Invalid(profile, "model", exception.Message);
        }

        if (model is null)
        {
            throw Invalid(profile, "model", $"'{profile.Model}' is not in the model catalog");
        }

        if (!string.Equals(model.Provider, provider.Kind, StringComparison.Ordinal))
        {
            throw Invalid(
                profile,
                "provider",
                $"'{provider.Name}' has kind '{provider.Kind}', but model '{model.Id}' belongs to '{model.Provider}'"
            );
        }

        ProviderApiFamily family;
        Uri? endpoint;
        try
        {
            // Precedence: profile override, then the already-merged discovery/catalog family,
            // then the provider's single family; the provider entry must declare a URL for it,
            // or its kind preset does (a preset-provided entry can stay minimal while the
            // preset's per-family URLs — System One among them — still route).
            family = ProviderFamilyResolver.Resolve(
                profile.ApiFamily ?? model.ApiFamily,
                provider.ChatFamilies,
                provider.Name,
                model.Id
            );
            endpoint =
                provider.UrlFor(family) ?? ProviderPresets.Find(provider.Kind)?.UrlFor(family);
        }
        catch (InvalidOperationException exception)
        {
            throw Invalid(profile, "apiFamily", exception.Message);
        }

        if (endpoint is null)
        {
            throw Invalid(
                profile,
                "apiFamily",
                $"provider '{provider.Name}' declares no URL for family '{ProviderApiFamilies.Name(family)}'; add \"baseUrls.{ProviderApiFamilies.Name(family)}\" to the provider entry"
            );
        }

        if (family == ProviderApiFamily.SystemOne)
        {
            RefuseSystemOneAlias(profile, model, catalog);
        }

        ValidateReasoning(profile, model);
        ValidateOutput(profile, model);
        ValidateTemperature(profile, model);
        int contextWindow = ValidateContextWindow(profile, model);
        ModelCapabilityDescriptor capabilities = (
            model.Capabilities ?? new ModelCapabilityDescriptor()
        )
            .Overlay(discoveredCapabilities)
            .Overlay(profile.CapabilitiesOverride);
        return new ResolvedModelProfile(
            profile,
            provider,
            model with
            {
                Endpoint = endpoint,
                ApiFamily = ProviderApiFamilies.Name(family),
                Capabilities = capabilities,
                // The entry's declared headers ride on the model: the credential pipeline merges
                // them with the kind preset's and renders the templates at send time. Values are
                // references and templates, never resolved secrets.
                ProviderHeaders = provider.Headers,
                CredentialPlacement = provider.PlacementFor(family).ToCompact(),
            },
            contextWindow,
            capabilities
        );
    }

    private static void ValidateReasoning(ModelProfile profile, ModelInfo model)
    {
        if (profile.ReasoningLevel is { } level)
        {
            if (level is not ("low" or "medium" or "high"))
            {
                throw Invalid(profile, "reasoningLevel", "must be low, medium, or high");
            }

            if (model.Reasoning is not ("effort" or "budget"))
            {
                throw Invalid(
                    profile,
                    "reasoningLevel",
                    $"model '{model.Id}' has no level control"
                );
            }
        }

        if (profile.ReasoningBudget is int budget)
        {
            if (
                budget <= 0
                || model.Reasoning != "budget"
                || model.MaxOutput is not int maximum
                || budget > maximum
            )
            {
                throw Invalid(
                    profile,
                    "reasoningBudget",
                    $"model '{model.Id}' does not support a positive token budget up to its maximum output"
                );
            }

            if (profile.ReasoningLevel is not null)
            {
                throw Invalid(profile, "reasoningBudget", "cannot be combined with reasoningLevel");
            }
        }
    }

    private static void ValidateOutput(ModelProfile profile, ModelInfo model)
    {
        if (profile.MaxOutput is int maximum)
        {
            if (maximum <= 0 || model.MaxOutput is not int supported || maximum > supported)
            {
                throw Invalid(
                    profile,
                    "maxOutput",
                    $"model '{model.Id}' does not support a positive limit up to {model.MaxOutput?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}"
                );
            }
        }
    }

    private static void ValidateTemperature(ModelProfile profile, ModelInfo model)
    {
        if (profile.Temperature is decimal temperature)
        {
            if (!model.SupportsTemperature || temperature is < 0m or > 2m)
            {
                throw Invalid(
                    profile,
                    "temperature",
                    $"model '{model.Id}' does not support this value"
                );
            }
        }
    }

    private static int ValidateContextWindow(ModelProfile profile, ModelInfo model)
    {
        if (profile.ContextWindow is not int window)
        {
            return model.ContextWindow;
        }

        if (window <= 0 || model.ContextWindow <= 0 || window > model.ContextWindow)
        {
            throw Invalid(
                profile,
                "contextWindow",
                $"must be positive and no greater than model '{model.Id}' window {model.ContextWindow.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            );
        }

        return window;
    }

    /// <summary>
    /// A System One profile names an exact version: an alias — a tilde-prefixed id or an id
    /// ending in <c>latest</c> — resolves to whatever the provider serves today, and a provider
    /// upgrade can change probabilities behind it. The refusal names the alias and the exact
    /// version the catalog records for it, so the fix is a one-word edit.
    /// </summary>
    private static void RefuseSystemOneAlias(
        ModelProfile profile,
        ModelInfo model,
        ModelCatalog catalog
    )
    {
        foreach (string alias in new[] { model.Id, model.WireId }.OfType<string>())
        {
            if (!IsAlias(alias))
            {
                continue;
            }

            string? resolved = ResolvedAliasVersion(catalog, model.Provider, alias);
            if (resolved is null)
            {
                throw Invalid(
                    profile,
                    "model",
                    $"'{alias}' is an alias; System One decision profiles must pin an exact version, "
                        + "and the catalog does not record which one it resolves to. Name the exact "
                        + "version the provider serves."
                );
            }

            throw Invalid(
                profile,
                "model",
                $"'{alias}' is an alias and currently resolves to '{resolved}'; pin that exact "
                    + "version, because a provider upgrade can change probabilities behind the alias."
            );
        }
    }

    /// <summary>
    /// The exact version behind an alias, from the catalog: the same provider's entry whose id or
    /// wire id extends the alias's non-<c>latest</c> prefix. Unknown when more than one entry
    /// competes or none exists — the refusal then says so instead of guessing.
    /// </summary>
    private static bool IsAlias(string id) =>
        id.StartsWith('~') || id.EndsWith("latest", StringComparison.Ordinal);

    private static string? ResolvedAliasVersion(
        ModelCatalog catalog,
        string providerKind,
        string alias
    )
    {
        if (!alias.EndsWith("latest", StringComparison.Ordinal))
        {
            string exact = alias.TrimStart('~');
            return catalog.Models.Any(candidate =>
                string.Equals(candidate.Provider, providerKind, StringComparison.Ordinal)
                && string.Equals(candidate.WireId ?? candidate.Id, exact, StringComparison.Ordinal)
            )
                ? exact
                : null;
        }

        string prefix = alias[..^"latest".Length].TrimStart('~').TrimEnd('-');
        if (prefix.Length == 0)
        {
            return null;
        }

        string[] versions =
        [
            .. catalog
                .Models.Where(candidate =>
                    string.Equals(candidate.Provider, providerKind, StringComparison.Ordinal)
                    && !candidate.Id.EndsWith("latest", StringComparison.Ordinal)
                )
                .Select(candidate => candidate.WireId ?? candidate.Id)
                .Distinct(StringComparer.Ordinal)
                .Where(version =>
                    version.StartsWith(prefix, StringComparison.Ordinal)
                    && version.Length > prefix.Length
                    && version[prefix.Length] is '.' or '-' or >= '0' and <= '9'
                ),
        ];
        return versions is [string version] ? version : null;
    }

    private static InvalidDataException Invalid(
        ModelProfile profile,
        string field,
        string reason
    ) => new($"Profile '{profile.Name}' has invalid '{field}': {reason}.");
}
