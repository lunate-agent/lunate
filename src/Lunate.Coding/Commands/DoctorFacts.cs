using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The individual probes behind <see cref="DoctorReport.Collect"/>. Each one is read-only: no
/// provider request, no container start, no update-check reservation, and no provider-store write.
/// </summary>
internal static class DoctorFacts
{
    internal static string StatePath(
        DoctorSeams seams,
        Func<string, string?> environment,
        List<string> problems
    )
    {
        if (seams.StatePath is { } configured)
        {
            return configured;
        }

        try
        {
            return LunateHome.Store;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            problems.Add(exception.Message);
            return Empty(environment(LunateHome.Variable)) ?? LunateHome.Profile;
        }
    }

    internal static AgentSettings? ReadSettings(
        string settingsPath,
        Func<string, string?> environment,
        List<string> problems
    )
    {
        try
        {
            return SettingsStore.Resolve(settingsPath, environment);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            problems.Add(exception.Message);
            return null;
        }
    }

    internal static (string? Provider, string? Model, DoctorCredential Credential) ResolveModel(
        AgentSettings? settings,
        string authPath,
        string providerStorePath,
        string? modelsPath,
        Func<string, string?> environment
    )
    {
        if (settings is null)
        {
            return (null, null, DoctorCredential.Absent);
        }

        try
        {
            // The read-only loads keep a report from writing providers.json through a migration.
            AuthStore auth = AuthStore.LoadReadOnly(authPath, environment);
            ProviderStore providers = ProviderStore.LoadReadOnly(
                providerStorePath,
                auth,
                settings.Providers
            );
            ModelCatalog catalog = HarnessFactory.LoadCatalog(settings, modelsPath);
            ChatModelSelection selection = ChatModelResolver.Resolve(
                settings,
                providers.Entries,
                catalog
            );
            string provider = selection.Profile?.Provider.Name ?? selection.Model.Provider;
            string reference = selection.Profile?.Provider.CredentialReference
                ?? (
                    selection.Model.AuthRef is { Length: > 0 } named
                        ? $"auth:{named}"
                        : $"auth:{provider}"
                );
            return (provider, selection.Model.Id, CredentialFor(reference, auth, environment));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A model is an optional fact: its absence must not change the required-path health.
            return (null, null, DoctorCredential.Absent);
        }
    }

    private static DoctorCredential CredentialFor(
        string reference,
        AuthStore auth,
        Func<string, string?> environment
    )
    {
        const string AuthPrefix = "auth:";
        const string EnvironmentPrefix = "env:";
        if (reference.StartsWith(AuthPrefix, StringComparison.Ordinal))
        {
            string name = reference[AuthPrefix.Length..];
            if (ProviderPresets.Find(name) is { } preset)
            {
                string? variable = PresentVariable(preset, environment);
                if (variable is not null)
                {
                    return new DoctorCredential(
                        "present",
                        FormattableString.Invariant($"environment ({variable})")
                    );
                }
            }

            return auth.TryGet(name) is null
                ? DoctorCredential.Absent
                : new DoctorCredential("present", "stored");
        }

        if (reference.StartsWith(EnvironmentPrefix, StringComparison.Ordinal))
        {
            string variable = reference[EnvironmentPrefix.Length..];
            return string.IsNullOrWhiteSpace(environment(variable))
                ? DoctorCredential.Absent
                : new DoctorCredential(
                    "present",
                    FormattableString.Invariant($"environment ({variable})")
                );
        }

        // A helper reference is accepted by the store schema but no host resolves one yet.
        return DoctorCredential.Absent;
    }

    private static string? PresentVariable(
        ProviderPreset preset,
        Func<string, string?> environment
    ) =>
        !string.IsNullOrWhiteSpace(environment(preset.KeyVariable)) ? preset.KeyVariable
        : preset.KeyVariableAlias is { } alias && !string.IsNullOrWhiteSpace(environment(alias))
            ? alias
        : null;

    internal static DoctorContainerEngines ContainerEngineFact(
        AgentSettings? settings,
        bool isWindows,
        Func<string, string?> environment,
        Func<string, bool>? executableAvailable
    )
    {
        Func<string, bool> available = executableAvailable
            ?? (name => OnPath(name, environment("PATH"), isWindows));
        return new DoctorContainerEngines(
            available(ExecutionEnvironmentSelection.Docker),
            available(ExecutionEnvironmentSelection.Podman),
            settings?.Environment?.Description
        );
    }

    internal static DoctorUpdateCheck UpdateCheckFact(string path, DateTimeOffset now)
    {
        UpdateCheckCache? cache = UpdateCheckCache.Read(path);
        return new DoctorUpdateCheck(
            cache?.CheckedAt,
            cache?.LatestVersion,
            cache?.ChangelogUrl,
            cache?.IsFresh(now) ?? false
        );
    }

    private static bool OnPath(string name, string? path, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string[] extensions = isWindows ? [".exe", ".cmd", ".bat", ""] : [""];
        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory.Trim(), name + extension);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool IsInteractive(Func<string, string?> environment) =>
        !Console.IsInputRedirected
        && !Console.IsOutputRedirected
        && !string.Equals(environment("TERM"), "dumb", StringComparison.OrdinalIgnoreCase);

    internal static int ConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    internal static string ColorName(Spectre.Console.ColorSystemSupport color) =>
        color switch
        {
            Spectre.Console.ColorSystemSupport.NoColors => "none",
            Spectre.Console.ColorSystemSupport.Legacy => "legacy",
            Spectre.Console.ColorSystemSupport.Standard => "standard",
            Spectre.Console.ColorSystemSupport.EightBit => "256",
            Spectre.Console.ColorSystemSupport.TrueColor => "truecolor",
            _ => "auto",
        };

    internal static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
