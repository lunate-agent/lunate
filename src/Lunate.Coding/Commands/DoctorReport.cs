using System.Runtime.InteropServices;
using Lunate.Tui;

namespace Lunate.Coding;

/// <summary>
/// The injectable facts of <c>lunate doctor</c>; production leaves every value null. The paths
/// name a state root and derive the files beneath it, so a test can report on a directory it owns
/// without moving the process's own <c>LUNATE_HOME</c>.
/// </summary>
internal sealed record DoctorSeams
{
    public string? StatePath { get; init; }

    public string? SettingsPath { get; init; }

    public string? AuthPath { get; init; }

    public string? ProviderStorePath { get; init; }

    public string? ModelsPath { get; init; }

    public string? UpdateCheckCachePath { get; init; }

    public Func<string, string?>? Environment { get; init; }

    public string? ProcessPath { get; init; }

    public string? BaseDirectory { get; init; }

    public bool? IsWindows { get; init; }

    public string? OsDescription { get; init; }

    public Architecture? Architecture { get; init; }

    public bool? IsInteractive { get; init; }

    public int? Columns { get; init; }

    public Func<string, bool>? ExecutableAvailable { get; init; }

    public Func<DateTimeOffset>? Clock { get; init; }

    public string? Version { get; init; }
}

internal sealed record DoctorPath(string Path, bool Exists);

internal sealed record DoctorCredential(string State, string? Source)
{
    internal static DoctorCredential Absent { get; } = new("absent", null);
}

internal sealed record DoctorContainerEngines(bool Docker, bool Podman, string? Selected);

internal sealed record DoctorTerminal(
    bool Interactive,
    string Color,
    bool Unicode,
    string? Term,
    int Columns
);

internal sealed record DoctorUpdateCheck(
    DateTimeOffset? CheckedAt,
    string? LatestVersion,
    string? ChangelogUrl,
    bool Fresh
);

/// <summary>One read-only view of the facts a bug report starts with.</summary>
internal sealed record DoctorReport(
    string Version,
    string Os,
    string Architecture,
    string InstallChannel,
    DoctorPath Config,
    DoctorPath State,
    string? Provider,
    string? Model,
    DoctorCredential Credential,
    DoctorContainerEngines ContainerEngines,
    DoctorTerminal Terminal,
    DoctorUpdateCheck UpdateCheck,
    IReadOnlyList<string> Problems
)
{
    internal bool Healthy => Problems.Count == 0;

    /// <summary>
    /// Collects every fact without a provider request, container start or update-check request.
    /// Only the required paths and a readable settings file decide <see cref="Problems"/>; an
    /// optional fact such as an engine, cache or model stays a report value.
    /// </summary>
    internal static DoctorReport Collect(DoctorSeams seams)
    {
        ArgumentNullException.ThrowIfNull(seams);
        Func<string, string?> environment = seams.Environment
            ?? System.Environment.GetEnvironmentVariable;
        bool isWindows = seams.IsWindows ?? OperatingSystem.IsWindows();
        var problems = new List<string>();

        string statePath = DoctorFacts.StatePath(seams, environment, problems);
        string settingsPath = seams.SettingsPath ?? Path.Combine(statePath, "settings.json");
        string authPath = seams.AuthPath ?? Path.Combine(statePath, "auth.json");
        string providerStorePath = seams.ProviderStorePath
            ?? Path.Combine(statePath, "providers.json");
        string updateCheckPath = seams.UpdateCheckCachePath
            ?? Path.Combine(statePath, "update-check.json");

        var state = new DoctorPath(statePath, Directory.Exists(statePath));
        var config = new DoctorPath(settingsPath, File.Exists(settingsPath));
        if (!state.Exists)
        {
            problems.Add(FormattableString.Invariant($"state path {statePath} is missing"));
        }

        if (!config.Exists)
        {
            problems.Add(FormattableString.Invariant($"config path {settingsPath} is missing"));
        }

        AgentSettings? settings = DoctorFacts.ReadSettings(settingsPath, environment, problems);
        (string? provider, string? model, DoctorCredential credential) = DoctorFacts.ResolveModel(
            settings,
            authPath,
            providerStorePath,
            seams.ModelsPath,
            environment
        );

        bool interactive = seams.IsInteractive ?? DoctorFacts.IsInteractive(environment);
        TerminalCapabilities capabilities = TerminalCapabilities.Detect(
            interactive,
            isWindows,
            environment
        );
        int columns = seams.Columns ?? (interactive ? DoctorFacts.ConsoleWidth() : 0);

        return new DoctorReport(
            Version: seams.Version ?? ProductVersion.Informational,
            Os: seams.OsDescription ?? RuntimeInformation.OSDescription,
            Architecture: (seams.Architecture ?? RuntimeInformation.OSArchitecture).ToString(),
            InstallChannel: InstallChannelDetector.Detect(
                seams.ProcessPath,
                seams.BaseDirectory ?? AppContext.BaseDirectory
            ),
            Config: config,
            State: state,
            Provider: provider,
            Model: model,
            Credential: credential,
            ContainerEngines: DoctorFacts.ContainerEngineFact(
                settings,
                isWindows,
                environment,
                seams.ExecutableAvailable
            ),
            Terminal: new DoctorTerminal(
                interactive,
                DoctorFacts.ColorName(capabilities.Color),
                capabilities.Unicode,
                DoctorFacts.Empty(environment("TERM")),
                columns
            ),
            UpdateCheck: DoctorFacts.UpdateCheckFact(
                updateCheckPath,
                (seams.Clock ?? (() => DateTimeOffset.UtcNow))()
            ),
            Problems: problems
        );
    }
}
