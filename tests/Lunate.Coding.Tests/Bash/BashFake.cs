using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>The probe fake, file-scope so sibling guard test classes reuse it.</summary>
internal static class Fake
{
    private const string ShellPath = "/bin/bash";

    internal static GuardProbe Unix(
        bool macOs,
        bool sandboxExec = false,
        string? bwrap = null,
        string? setpriv = null,
        string bwrapOutput = "",
        string setprivOutput = "",
        bool? apparmorRestrictsUserns = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? directoryChildren = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fileChildren = null,
        IReadOnlyList<string>? profileChildren = null,
        IReadOnlyList<string>? localChildren = null,
        bool authExists = false,
        bool shellExists = true,
        bool isWindows = false,
        Func<string, bool>? directoryExists = null
    )
    {
        string? byName(string name) =>
            (name, sandboxExec, bwrap, setpriv) switch
            {
                ("sandbox-exec", true, _, _) => "/usr/bin/sandbox-exec",
                ("bwrap", _, not null, _) => "/usr/bin/bwrap",
                ("setpriv", _, _, not null) => "/usr/bin/setpriv",
                _ => null,
            };

        return new GuardProbe(
            IsWindows: isWindows,
            IsMacOs: macOs,
            IsLinux: !macOs && !isWindows,
            FindOnPath: byName,
            FileExists: path =>
                path == ShellPath
                    ? shellExists
                    : sandboxExec && path == "/usr/bin/sandbox-exec"
                        || authExists && path.EndsWith("auth.json", StringComparison.Ordinal),
            DirectoryExists: directoryExists ?? (_ => true),
            GetEnvironmentVariable: _ => null,
            ReadTextFile: _ =>
                apparmorRestrictsUserns is not { } value ? null
                : value ? "1"
                : "0",
            ListDirectories: path =>
                directoryChildren is not null && directoryChildren.TryGetValue(path, out var mapped)
                    ? mapped
                : path.EndsWith(".local", StringComparison.Ordinal) ? localChildren ?? []
                : path is "/tmp" or "/var/tmp" ? []
                : profileChildren ?? [],
            ListFiles: path =>
                fileChildren is not null && fileChildren.TryGetValue(path, out var mapped)
                    ? mapped
                    : [],
            Run: (fileName, _) =>
                fileName.Contains("bwrap", StringComparison.Ordinal)
                    ? (bwrapOutput.Length == 0 ? 0 : 1, bwrapOutput)
                : fileName.Contains("setpriv", StringComparison.Ordinal)
                    ? (setprivOutput.Length == 0 ? 0 : 1, setprivOutput)
                : (0, "")
        );
    }
}
