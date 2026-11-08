using System.Globalization;
using System.Runtime.InteropServices;

namespace S7;

/// <summary>
/// The platform facts around the run: engine, machine, images, mounts, kernel and ownership. The
/// spike's feasibility answers are macOS/Linux/Windows questions, so the evidence has to name the
/// machine it was taken on — a container on macOS is a VM, a container on Linux is not.
/// </summary>
internal static class EnvironmentEvidence
{
    internal static int Run(SpikeOptions options, EvidenceWriter writer)
    {
        var engine = new Engine(options);

        writer.Section("host");
        writer.Key("os", RuntimeInformation.OSDescription);
        writer.Key("arch", RuntimeInformation.OSArchitecture.ToString());
        writer.Key("runtime", RuntimeInformation.FrameworkDescription);
        writer.Key("workspace", options.Workspace);
        writer.Key("temp", Path.GetTempPath());

        writer.Section("engine");
        writer.Check("the engine CLI is on PATH", engine.Available(), options.Engine);
        ProcResult version = engine.Version();
        writer.Line(
            string.Join(
                "\n",
                version
                    .StdoutTrimmed.Split('\n')
                    .Where(line =>
                        line.Contains("Version", StringComparison.Ordinal)
                        || line.Contains("OS/Arch", StringComparison.Ordinal)
                        || line.Contains("Server", StringComparison.Ordinal)
                        || line.Contains("Client", StringComparison.Ordinal)
                    )
            )
        );

        writer.Section("machine (a macOS container runs in a VM, a Linux one does not)");
        writer.Line(engine.MachineList().StdoutTrimmed);

        writer.Section("images");
        foreach (string line in engine.Images().StdoutTrimmed.Split('\n'))
        {
            if (
                line.Contains("dotnet/sdk", StringComparison.Ordinal)
                || line.Contains("debian", StringComparison.Ordinal)
            )
            {
                writer.Line(line);
            }
        }

        writer.Section("the evidence container");
        bool running = engine.Running(options.Container);
        writer.Check(
            $"the container {options.Container} is running",
            running,
            running ? "running" : "not running — scripts/run-evidence.sh starts it"
        );
        writer.Line(engine.Inspect(options.Container).StdoutTrimmed);

        writer.Section("inside the container");
        writer.Line(
            engine.ExecText(
                options.Container,
                "cat /etc/os-release | head -2; echo \"arch=$(uname -m) kernel=$(uname -r)\"; "
                    + "echo \"uid=$(id -u) gid=$(id -g)\"; echo \"tools:\"; "
                    + "for tool in bash git dotnet node python3 bwrap setpriv sandbox-exec; do "
                    + "printf '  %-12s %s\\n' \"$tool\" \"$(command -v $tool || echo '<absent>')\"; done; "
                    + "echo \"user namespaces: $(cat /proc/sys/kernel/unprivileged_userns_clone 2>/dev/null || echo '<no such knob>')\""
            )
        );

        writer.Section("the same file, both sides");
        string relative = "AGENTS.md";
        string absolute = Path.Combine(
            options.Workspace,
            relative.Replace('/', Path.DirectorySeparatorChar)
        );
        writer.Key(
            "host bytes",
            File.Exists(absolute)
                ? new FileInfo(absolute).Length.ToString(CultureInfo.InvariantCulture)
                : "missing"
        );
        writer.Line(
            engine.ExecText(
                options.Container,
                $"stat -c 'container: size=%s uid=%u gid=%g mode=%a' {Engine.Quote(relative)}"
            )
        );
        return writer.Finish();
    }
}
