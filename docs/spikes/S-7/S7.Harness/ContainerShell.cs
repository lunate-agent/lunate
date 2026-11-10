using System.Text;

namespace S7;

/// <summary>
/// The container shell: a host shim script that <c>BashTool</c> is pointed at through
/// <see cref="Lunate.Coding.ShellResolver"/>'s public override path, and that runs every command in
/// the container instead of on the host. The product's bash tool therefore runs unmodified — its
/// guard wrapping, output cap, timeout, tree kill and change scan all stay in the path — and only
/// the last hop (which executable receives the command) changes. That is exactly the seam #626 has
/// to make a first-class thing.
/// </summary>
internal sealed class ContainerShell
{
    private readonly SpikeOptions _options;

    internal ContainerShell(SpikeOptions options)
    {
        _options = options;
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "s7-container-shell.sh");
    }

    /// <summary>The host path of the shim; it exists on the host only, never inside the container.</summary>
    internal string Path { get; }

    /// <summary>Writes the shim; returns the file it wrote so the evidence can quote it.</summary>
    internal string Write()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.Workspace);
        var script = new StringBuilder();
        script.AppendLine("#!/bin/sh");
        script.AppendLine("# S-7 spike (#625): the \"container shell\" BashTool is pointed at.");
        script.AppendLine(
            "# Every command the tool would run locally runs in the container instead."
        );
        script.AppendLine(
            $"exec {_options.Engine} exec -i -w {Engine.Quote(_options.Workspace)} "
                + $"{_options.Container} /bin/bash \"$@\""
        );
        File.WriteAllText(Path, script.ToString(), new UTF8Encoding(false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                Path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        return File.ReadAllText(Path);
    }

    /// <summary>The resolver the bash tool takes; the override path is the public settings seam.</summary>
    internal Lunate.Coding.ShellResolver Resolver() => new(Path);
}
