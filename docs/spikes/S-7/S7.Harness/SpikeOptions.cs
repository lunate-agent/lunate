using System.Globalization;

namespace S7;

/// <summary>The spike's knobs: all defaults come from the environment so a rerun needs no flags.</summary>
internal sealed record SpikeOptions
{
    internal required string Mode { get; init; }

    /// <summary>The container CLI to drive: <c>podman</c> unless S7_ENGINE says otherwise.</summary>
    internal string Engine { get; init; } = "podman";

    /// <summary>The long-lived container the evidence runs in.</summary>
    internal string Container { get; init; } = "lunate-s7-evidence";

    /// <summary>The workspace: bind-mounted into the container at the same absolute path.</summary>
    internal required string Workspace { get; init; }

    /// <summary>The image the script starts the container from.</summary>
    internal string Image { get; init; } = "docker.io/library/debian:stable-slim";

    /// <summary>How many samples each timing row takes.</summary>
    internal int Runs { get; init; } = 15;

    internal static SpikeOptions Parse(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "help";
        var options = new SpikeOptions
        {
            Mode = mode,
            Engine = Environment.GetEnvironmentVariable("S7_ENGINE") ?? "podman",
            Container = Environment.GetEnvironmentVariable("S7_CONTAINER") ?? "lunate-s7-evidence",
            Workspace =
                Environment.GetEnvironmentVariable("S7_WORKSPACE")
                ?? Directory.GetCurrentDirectory(),
            Image =
                Environment.GetEnvironmentVariable("S7_IMAGE")
                ?? "docker.io/library/debian:stable-slim",
            Runs = int.Parse(
                Environment.GetEnvironmentVariable("S7_RUNS") ?? "15",
                CultureInfo.InvariantCulture
            ),
        };

        for (int index = 1; index < args.Length; index++)
        {
            string name = args[index];
            string Value()
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException($"{name} needs a value", nameof(args));
                }

                return args[++index];
            }

            options = name switch
            {
                "--engine" => options with { Engine = Value() },
                "--container" => options with { Container = Value() },
                "--workspace" => options with { Workspace = Path.GetFullPath(Value()) },
                "--image" => options with { Image = Value() },
                "--runs" => options with
                {
                    Runs = int.Parse(Value(), CultureInfo.InvariantCulture),
                },
                _ => throw new ArgumentException($"unknown option {name}", nameof(args)),
            };
        }

        options = options with { Workspace = Path.GetFullPath(options.Workspace) };
        return options;
    }

    internal string EvidenceDirectory => Path.Combine(Workspace, ".s7-evidence");

    internal string Full(string relative) =>
        Path.Combine(Workspace, relative.Replace('/', Path.DirectorySeparatorChar));
}

/// <summary>
/// The evidence writer: one line per fact, one verdict per acceptance check, and a non-zero exit
/// when any check failed — a spike run that disagrees with the write-up must not read as green.
/// </summary>
internal sealed class EvidenceWriter(TextWriter output)
{
    private int _checks;
    private int _failures;

    internal void Line(string text) => output.WriteLine(text);

    internal void Section(string title)
    {
        output.WriteLine();
        output.WriteLine($"== {title} ==");
    }

    internal void Key(string key, string value) => Line($"{key}: {value}");

    internal void Check(string name, bool ok, string detail)
    {
        _checks++;
        if (!ok)
        {
            _failures++;
        }

        output.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
    }

    internal int Finish()
    {
        output.WriteLine();
        output.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"checks: {_checks}, failures: {_failures}")
        );
        return _failures == 0 ? 0 : 1;
    }
}
