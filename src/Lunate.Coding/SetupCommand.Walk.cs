using System.Globalization;
using Lunate.Ai;

namespace Lunate.Coding;

// The flag path's walk assembly (tasks 4.2–4.4): the answers become the same staged model the
// interactive driver walks, the checks run through the same discovery client and model check the
// driver uses, and the commit is SetupWizardCommit — one writer, never a second emitter. Any
// answer that would need interaction exits 2 instead of hanging; absent flags skip steps and
// invent nothing. A run whose staged writes are all skips is a no-op, not a rewrite.

internal static partial class SetupCommand
{
    private static async Task<int> WalkAsync(
        FlagAnswers answers,
        SetupCommandSeams seams,
        TextWriter output,
        TextWriter errors,
        CancellationToken ct
    )
    {
        if (answers.Endpoint is not null && answers.Provider is null)
        {
            throw new UsageException(
                "the custom endpoint needs --provider <id>: the provider names the entry everywhere"
            );
        }

        if (answers.Fast && answers.AsDecision)
        {
            throw new UsageException(
                "--fast and --as-decision are contradictory: the written profile cannot bind both "
                    + "usages; run setup twice"
            );
        }

        var environment = new StoreEnvironment(
            seams.Environment ?? System.Environment.GetEnvironmentVariable
        );
        string settingsPath = seams.SettingsPath ?? AuthStore.SettingsFilePath(seams.AuthPath);
        string providerPath = AuthStore.ProviderStoreFilePath(seams.AuthPath);
        AuthStore auth = AuthStore.Load(seams.AuthPath, environment.Resolve);
        AgentSettings settings = SettingsStore.Resolve(settingsPath, environment.Resolve);
        ProviderStore providers = ProviderStore.Load(providerPath, auth, settings.Providers);

        if (!answers.Force)
        {
            if (
                answers.Provider is { } wanted
                && providers.Entries.Any(entry =>
                    string.Equals(entry.Name, wanted, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                errors.WriteLine(
                    $"lunate: provider entry '{wanted}' already exists; pass --force to replace it"
                );
                return ExitUsage;
            }

            if (answers.Profile is { } profile && settings.Profiles.ContainsKey(profile))
            {
                errors.WriteLine(
                    $"lunate: profile '{profile}' already exists; pass --force to replace it"
                );
                return ExitUsage;
            }
        }

        var wizard = new SetupWizardModel();
        WizardDiscoveryResult? discovery = null;
        if (answers.Provider is { } providerId)
        {
            wizard.RecordProvider(ProviderChoice(answers, providerId));
            wizard.RecordCredential(
                await CredentialChoice(answers, providerId, environment, seams, ct)
                    .ConfigureAwait(false)
            );
            if (!answers.NoTest)
            {
                WizardDiscoveryResult result = await WizardDiscovery
                    .RunAsync(
                        wizard.Provider!,
                        wizard.Credential,
                        environment.Resolve,
                        auth.TryGet,
                        seams.DiscoveryTransport,
                        ct
                    )
                    .ConfigureAwait(false);
                discovery = result;
                wizard.RecordConnectionTest(
                    result.Passed
                        ? new WizardCheckResult(WizardProgress.Passed)
                        : new WizardCheckResult(
                            WizardProgress.Failed,
                            FailureShape(result.FailureKind)
                        )
                );
            }
            else
            {
                wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Skipped));
            }
        }

        ModelCatalog catalog = HarnessFactory.LoadCatalog(settings, seams.ModelsPath);
        if (answers.Model is { } modelId)
        {
            wizard.RecordModel(
                new WizardModelChoice(
                    modelId,
                    FamilyOf(answers, wizard, discovery, modelId, catalog)
                )
            );
        }

        DecideAnswers(answers, wizard, catalog);
        IChatClientFactory factory = seams.ChatFactory ?? HarnessFactory.CreateFactory(auth);
        if (wizard.Model is not null && wizard.Provider is not null)
        {
            if (answers.NoTest || answers.NoModelCheck)
            {
                // `--no-test` is the switch a script reaches for to keep a run offline: it skips
                // the connection test and the paid model check together, and the summary names
                // the model unverified. `--no-model-check` stays the narrower switch that skips
                // only the check (S-19).
                wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Skipped));
            }
            else
            {
                WizardCheckResult check = await WizardModelCheck
                    .RunAsync(
                        wizard,
                        catalog,
                        factory,
                        auth,
                        environment.Resolve,
                        () => ResolveCredential(wizard.Credential, auth, environment.Resolve),
                        context: null,
                        ct
                    )
                    .ConfigureAwait(false);
                wizard.RecordModelCheck(check);
            }
        }

        // A decision profile is verified by its own check: one tiny noul through the decision
        // client, not a chat completion (spec: The full walk, step 5). `--no-test` and
        // `--no-model-check` skip it with the rest, and the summary then names it unverified.
        if (wizard.DecisionProfile is not null)
        {
            if (answers.NoTest || answers.NoModelCheck)
            {
                wizard.RecordDecisionCheck(new WizardCheckResult(WizardProgress.Skipped));
            }
            else
            {
                WizardCheckResult decisionCheck = await WizardModelCheck
                    .RunDecisionAsync(
                        wizard,
                        catalog,
                        factory,
                        auth,
                        environment.Resolve,
                        () => ResolveCredential(wizard.Credential, auth, environment.Resolve),
                        context: null,
                        ct
                    )
                    .ConfigureAwait(false);
                wizard.RecordDecisionCheck(decisionCheck);
            }
        }

        foreach (string line in SetupWizardSummary.Render(wizard))
        {
            output.WriteLine(line);
        }

        if (!wizard.HasStagedWrites)
        {
            output.WriteLine("written: nothing — every step was skipped");
            return ExitOk;
        }

        SetupWizardCommit.Commit(
            wizard,
            new SetupWizardCommit.CommitPaths(settingsPath, providerPath),
            () => auth,
            () => providers.Entries
        );
        foreach (string written in WrittenLines(wizard))
        {
            output.WriteLine(written);
        }

        return ExitOk;
    }

    /// <summary>The provider answer: the preset id's row, or the custom path's entry. An unknown
    /// family or an unusable URL is refused by name before anything is staged.</summary>
    private static WizardProviderChoice ProviderChoice(FlagAnswers answers, string providerId)
    {
        if (answers.Endpoint is { } endpointText)
        {
            if (
                !CustomProvider.TryParseEndpoint(
                    endpointText,
                    out Uri? endpoint,
                    out string? endpointError
                )
            )
            {
                throw new UsageException($"the endpoint was refused: {endpointError}");
            }

            string family = answers.ApiFamily ?? "openai-chat";
            if (!ProviderApiFamilies.TryParse(family, out ProviderApiFamily parsedFamily))
            {
                throw new UsageException(
                    $"API family '{family}' is not a supported family; supported families: "
                        + $"{ProviderApiFamilies.Supported()}"
                );
            }

            CredentialPlacement placement = PlacementOf(answers.CredentialPlacement, parsedFamily);
            Uri? modelsUrl = answers.ModelsUrl is { } declaredModelsUrl
                ? OptionalUrl("models URL", declaredModelsUrl)
                : null;
            return new WizardProviderChoice(
                providerId,
                Custom: true,
                Region: null,
                endpoint,
                family,
                placement,
                modelsUrl
            );
        }

        if (ProviderPresets.Find(providerId) is not { } preset)
        {
            throw new UsageException(
                $"'{providerId}' is not a provider preset and no --endpoint was given; the wizard's "
                    + $"presets are: {string.Join(", ", ProviderPresets.All.Select(row => row.Id))}"
            );
        }

        ProviderApiFamily chosenFamily = preset.DefaultFamily;
        if (answers.ApiFamily is { } familyText)
        {
            if (
                !ProviderApiFamilies.TryParse(familyText, out ProviderApiFamily named)
                || !preset.Families.Contains(named)
            )
            {
                throw new UsageException(
                    $"provider '{preset.Id}' does not declare the API family '{familyText}'; its "
                        + $"families are: {FamiliesOf(preset)}"
                );
            }

            chosenFamily = named;
        }

        return new WizardProviderChoice(
            preset.Id,
            Custom: false,
            Region: null,
            Endpoint: null,
            ProviderApiFamilies.Name(chosenFamily),
            PlacementOf(answers.CredentialPlacement, chosenFamily, preset),
            answers.ModelsUrl is { } presetModelsUrl
                ? OptionalUrl("models URL", presetModelsUrl)
                : preset.ModelsUrl
        );
    }

    private static string FamiliesOf(ProviderPreset preset) =>
        string.Join(", ", preset.Families.Select(ProviderApiFamilies.Name));

    private static CredentialPlacement PlacementOf(
        string? declared,
        ProviderApiFamily family,
        ProviderPreset? preset = null
    )
    {
        if (declared is { } value)
        {
            if (!CredentialPlacement.TryParseCompact(value, out CredentialPlacement? parsed, out _))
            {
                throw new UsageException(
                    $"'{value}' is not a credential placement: declare 'bearer' or 'x-api-key'"
                );
            }

            return parsed!;
        }

        return preset is { } row ? row.PlacementFor(family)
            : family == ProviderApiFamily.AnthropicMessages ? CredentialPlacement.XApiKey
            : CredentialPlacement.Bearer;
    }

    private static Uri? OptionalUrl(string what, string value)
    {
        if (!CustomProvider.TryParseEndpoint(value, out Uri? url, out string? error))
        {
            throw new UsageException($"the {what} was refused: {error}");
        }

        return url;
    }

    /// <summary>The credential answer: a flag-supplied reference, a piped key read once, or the
    /// provider's standard environment variable when it is already set (the wizard's first offer).
    /// A run with none of the three exits 2 and names the credential.</summary>
    private static async Task<WizardCredentialChoice> CredentialChoice(
        FlagAnswers answers,
        string providerId,
        StoreEnvironment environment,
        SetupCommandSeams seams,
        CancellationToken ct
    )
    {
        switch (answers.Credential, answers.KeyStdin)
        {
            case ({ } reference, false):
                return ReferenceChoice(reference, providerId);
            case (null, true):
            {
                string? line;
                try
                {
                    line = await (seams.Input ?? System.Console.In)
                        .ReadLineAsync(ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    throw new UsageException(
                        "--key-stdin read no key: pipe exactly one line with the key text"
                    );
                }

                return new WizardCredentialChoice(providerId, line.Trim());
            }
            case (not null, true):
                throw new UsageException(
                    "--credential and --key-stdin are mutually exclusive: name one answer, use the "
                        + "other only when stdin is a pipe"
                );
            default:
                break;
        }

        if (ProviderPresets.Find(providerId) is { } preset)
        {
            string chosenVariable =
                !string.IsNullOrWhiteSpace(environment.Resolve(preset.KeyVariable))
                    ? preset.KeyVariable
                : preset.KeyVariableAlias is { } alias
                && !string.IsNullOrWhiteSpace(environment.Resolve(alias))
                    ? alias
                : string.Empty;
            if (chosenVariable.Length > 0)
            {
                return new WizardCredentialChoice($"env:{chosenVariable}");
            }
        }

        if (ProviderPresets.Find(providerId) is null)
        {
            throw new UsageException(
                $"the credential for the custom provider '{providerId}' needs interaction or a flag: "
                    + "use --credential <env:VARIABLE|auth:name> or pipe one line in with --key-stdin"
            );
        }

        throw new UsageException(
            $"the credential for '{providerId}' needs interaction or a flag: none of "
                + $"{ProviderPresets.Find(providerId)!.KeyVariable} is set, and neither "
                + "--credential <env:VARIABLE|auth:name> nor --key-stdin was given"
        );
    }

    private static WizardCredentialChoice ReferenceChoice(string reference, string providerId)
    {
        reference = reference.Trim();
        static bool HasTarget(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(value[prefix.Length..]);

        if (!HasTarget(reference, "env:") && !HasTarget(reference, "auth:"))
        {
            throw new UsageException(
                "a credential reference is 'env:<VARIABLE>' or 'auth:<name>'; a typed key is not a "
                    + "flag value — pipe it in with --key-stdin"
            );
        }

        return new WizardCredentialChoice(reference);
    }

    /// <summary>
    /// The model's API family, in the ADR-0029 order: the <c>--api-family</c> flag (validated
    /// against the preset's families when it was staged), then the discovery listing's per-model
    /// family, then the catalog's, then the preset's default — so the family the check uses is the
    /// family the profile and the provider entry record (S-12).
    /// </summary>
    private static string FamilyOf(
        FlagAnswers answers,
        SetupWizardModel wizard,
        WizardDiscoveryResult? discovery,
        string modelId,
        ModelCatalog catalog
    )
    {
        if (answers.ApiFamily is { } declared)
        {
            return declared;
        }

        WizardProviderChoice provider = wizard.Provider!;
        if (provider.Custom)
        {
            return provider.ApiFamily;
        }

        if (
            discovery?.Models.FirstOrDefault(model =>
                string.Equals(model.Id, modelId, StringComparison.Ordinal)
            ) is
            { ApiFamily: { Length: > 0 } listed }
        )
        {
            return listed;
        }

        return
            WizardModelLookup.Resolve(provider, catalog, modelId).Match?.ApiFamily
                is { Length: > 0 } catalogued
            ? catalogued
            : provider.ApiFamily;
    }

    /// <summary>The profile flags: the name must parse the way the settings parser parses one,
    /// and a thinking level is only offered when the model declares a reasoning style.</summary>
    private static void DecideAnswers(
        FlagAnswers answers,
        SetupWizardModel wizard,
        ModelCatalog catalog
    )
    {
        if (
            !answers.Fast
            && answers.Profile is null
            && answers.Thinking is null
            && !answers.AsDecision
        )
        {
            return;
        }

        if (wizard.Model is not { } model)
        {
            throw new UsageException(
                "the profile flags need --model <id>: no model value is invented, so an unanswered "
                    + "model step cannot carry a profile"
            );
        }

        if (answers.Profile is { } profileName)
        {
            ValidateProfileName(profileName);
            if (answers.AsDecision)
            {
                if (answers.Thinking is { } decisionThinking)
                {
                    throw new UsageException(
                        $"--as-decision binds the profile to decisions.profile in place of chat, so "
                            + "--thinking {0} has no chat profile to apply to".Replace(
                                "{0}",
                                decisionThinking
                            )
                    );
                }

                wizard.RecordDecisionProfile(
                    new WizardDecisionChoice(
                        profileName,
                        model.ModelId,
                        Native: false,
                        PinnedVersion: null
                    )
                );
                return;
            }

            string? level = null;
            if (answers.Thinking is { } declared && declared != "off")
            {
                if (declared is not ("low" or "medium" or "high"))
                {
                    throw new UsageException(
                        $"--thinking {declared} is not a level: use off, low, medium or high"
                    );
                }

                if (
                    WizardModelCheck.ModelFor(wizard, model, catalog).Reasoning
                    is not ("effort" or "budget")
                )
                {
                    throw new UsageException(
                        $"'--thinking {declared}' is refused: the model '{model.ModelId}' declares "
                            + "no reasoning style, so the profile cannot carry a thinking level"
                    );
                }

                level = declared;
            }

            wizard.RecordChatProfile(new WizardProfileChoice(profileName, model.ModelId, level));
        }

        if (answers.Fast)
        {
            wizard.RecordFastProfile(true);
        }
    }

    private static void ValidateProfileName(string name)
    {
        if (name.Length == 0 || name.Any(char.IsWhiteSpace) || name.Contains('/'))
        {
            throw new UsageException(
                $"the profile name '{name}' is refused: it must be non-empty, without whitespace "
                    + "or '/', exactly as the settings parser parses one"
            );
        }
    }

    /// <summary>The check step's failure shape, the same words the interactive driver prints.</summary>
    private static string FailureShape(WizardFailureKind? kind) =>
        kind switch
        {
            WizardFailureKind.Network => "network",
            WizardFailureKind.Authentication => "authentication",
            WizardFailureKind.Rejected => "rejected status",
            _ => "undecipherable response",
        };

    /// <summary>Never a credential: names each store the commit touched, or the bindings it made.</summary>
    private static IReadOnlyList<string> WrittenLines(SetupWizardModel wizard)
    {
        List<string> written = [];
        if (wizard.Provider is { } provider)
        {
            written.Add($"written: provider entry '{provider.Id}'");
            if (wizard.Credential is { } credential)
            {
                written.Add(
                    credential.Key is not null
                        ? $"written: the key stored under '{provider.Id}' in auth.json"
                        : $"written: credential reference {credential.Reference}"
                );
            }
        }

        if (wizard.ChatProfile is { } chat)
        {
            written.Add(
                "written: profile '"
                    + chat.Name
                    + "' (model "
                    + chat.ModelId
                    + (
                        chat.ReasoningLevel is { } level
                            ? string.Format(CultureInfo.InvariantCulture, ", thinking {0}", level)
                            : string.Empty
                    )
                    + ")"
            );
            written.Add($"written: usages.chat → {chat.Name}");
            if (wizard.FastProfile)
            {
                written.Add($"written: usages.fast → {chat.Name}");
            }
        }

        if (wizard.DecisionProfile is { } decision && wizard.ChatProfile is null)
        {
            written.Add($"written: decisions.profile → {decision.ProfileName}");
        }

        return written;
    }

    /// <summary>The same credential resolution the interactive wizard's check uses: the typed
    /// key, then an env variable, then a stored key. The value is resolved only into the client,
    /// never printed, never logged, never written to a store file.</summary>
    internal static string? ResolveCredential(
        WizardCredentialChoice? credential,
        AuthStore auth,
        Func<string, string?> environment
    )
    {
        if (credential is null)
        {
            return null;
        }

        if (credential.Key is { Length: > 0 } key)
        {
            return key;
        }

        return credential.Reference.StartsWith("env:", StringComparison.Ordinal)
                ? environment(credential.Reference["env:".Length..])
            : credential.Reference.StartsWith("auth:", StringComparison.Ordinal)
                ? auth.TryGet(credential.Reference["auth:".Length..])
            : null;
    }

    private sealed record StoreEnvironment(Func<string, string?> Resolve);
}
