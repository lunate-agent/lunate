using System.Globalization;
using System.Text.Json.Nodes;

namespace Lunate.Coding;

/// <summary>
/// The two renderings of one <see cref="DoctorReport"/>. Both are built from the same record, so a
/// fact cannot appear in one view and vanish from the other; every text value is formatted with
/// <see cref="CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class DoctorView
{
    internal static void WriteText(DoctorReport report, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine(FormattableString.Invariant($"version: {report.Version}"));
        output.WriteLine(FormattableString.Invariant($"os: {report.Os}"));
        output.WriteLine(FormattableString.Invariant($"architecture: {report.Architecture}"));
        output.WriteLine(FormattableString.Invariant($"install channel: {report.InstallChannel}"));
        WritePath("config", report.Config, output);
        WritePath("state", report.State, output);
        output.WriteLine(
            FormattableString.Invariant($"provider: {report.Provider ?? "not resolved"}")
        );
        output.WriteLine(FormattableString.Invariant($"model: {report.Model ?? "not configured"}"));
        output.WriteLine(
            FormattableString.Invariant($"credential: {CredentialText(report.Credential)}")
        );
        output.WriteLine(
            FormattableString.Invariant(
                $"container engines: {ContainerText(report.ContainerEngines)}"
            )
        );
        output.WriteLine(FormattableString.Invariant($"terminal: {TerminalText(report.Terminal)}"));
        output.WriteLine(
            FormattableString.Invariant($"update check: {UpdateCheckText(report.UpdateCheck)}")
        );
        output.WriteLine(
            FormattableString.Invariant($"health: {(report.Healthy ? "healthy" : "unhealthy")}")
        );
        foreach (string problem in report.Problems)
        {
            output.WriteLine(FormattableString.Invariant($"problem: {problem}"));
        }
    }

    internal static void WriteJson(DoctorReport report, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var problems = new JsonArray();
        foreach (string problem in report.Problems)
        {
            problems.Add(problem);
        }

        var root = new JsonObject
        {
            ["version"] = report.Version,
            ["os"] = report.Os,
            ["architecture"] = report.Architecture,
            ["installChannel"] = report.InstallChannel,
            ["config"] = PathJson(report.Config),
            ["state"] = PathJson(report.State),
            ["provider"] = Text(report.Provider),
            ["model"] = Text(report.Model),
            ["credential"] = new JsonObject
            {
                ["state"] = report.Credential.State,
                ["source"] = Text(report.Credential.Source),
            },
            ["containerEngines"] = new JsonObject
            {
                ["docker"] = report.ContainerEngines.Docker,
                ["podman"] = report.ContainerEngines.Podman,
                ["selected"] = Text(report.ContainerEngines.Selected),
            },
            ["terminal"] = new JsonObject
            {
                ["interactive"] = report.Terminal.Interactive,
                ["color"] = report.Terminal.Color,
                ["unicode"] = report.Terminal.Unicode,
                ["term"] = Text(report.Terminal.Term),
                ["columns"] = report.Terminal.Columns,
            },
            ["updateCheck"] = new JsonObject
            {
                ["checkedAt"] = Text(
                    report.UpdateCheck.CheckedAt?.ToString("O", CultureInfo.InvariantCulture)
                ),
                ["latestVersion"] = Text(report.UpdateCheck.LatestVersion),
                ["changelogUrl"] = Text(report.UpdateCheck.ChangelogUrl),
                ["fresh"] = report.UpdateCheck.Fresh,
            },
            ["healthy"] = report.Healthy,
            ["problems"] = problems,
        };
        output.WriteLine(root.ToJsonString());
    }

    private static JsonObject PathJson(DoctorPath path) =>
        new() { ["path"] = path.Path, ["exists"] = path.Exists };

    private static JsonNode? Text(string? value) => value is null ? null : JsonValue.Create(value);

    private static void WritePath(string label, DoctorPath path, TextWriter output) =>
        output.WriteLine(
            FormattableString.Invariant(
                $"{label}: {path.Path} ({(path.Exists ? "present" : "missing")})"
            )
        );

    private static string CredentialText(DoctorCredential credential) =>
        credential.State == "present" && credential.Source is { Length: > 0 } source
            ? FormattableString.Invariant($"present ({source})")
            : credential.State;

    private static string ContainerText(DoctorContainerEngines engines)
    {
        string selected = engines.Selected is { Length: > 0 } value
            ? FormattableString.Invariant($"selected {value}")
            : "none selected";
        return FormattableString.Invariant(
            $"docker {(engines.Docker ? "present" : "absent")}, podman {(engines.Podman ? "present" : "absent")}; {selected}"
        );
    }

    private static string TerminalText(DoctorTerminal terminal)
    {
        string mode = terminal.Interactive ? "interactive" : "non-interactive";
        string term = terminal.Term is { Length: > 0 } value
            ? FormattableString.Invariant($"TERM={value}")
            : "TERM unset";
        string columns =
            terminal.Columns > 0
                ? FormattableString.Invariant($"{terminal.Columns} columns")
                : "columns unknown";
        return FormattableString.Invariant(
            $"{mode}, color {terminal.Color}, unicode {(terminal.Unicode ? "yes" : "no")}, {columns}, {term}"
        );
    }

    private static string UpdateCheckText(DoctorUpdateCheck update)
    {
        if (update.CheckedAt is not { } checkedAt)
        {
            return "never checked";
        }

        string stamp = checkedAt.ToString("O", CultureInfo.InvariantCulture);
        if (update.LatestVersion is { Length: > 0 } version)
        {
            return FormattableString.Invariant(
                $"latest {version} (checked {stamp}, {(update.Fresh ? "fresh" : "stale")})"
            );
        }

        return FormattableString.Invariant($"no answer (checked {stamp})");
    }
}
