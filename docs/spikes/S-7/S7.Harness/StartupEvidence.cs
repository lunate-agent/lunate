using System.Globalization;
using Lunate.Agent;
using Lunate.Coding;

namespace S7;

/// <summary>
/// The startup-cost evidence: what one tool call costs locally, what the same call costs through
/// the container, and what a cold container costs on top. The rows are the same product code
/// (<see cref="BashTool"/>) with only the shell seam moved, plus the raw CLI calls underneath it.
/// </summary>
internal static class StartupEvidence
{
    internal static async Task<int> RunAsync(SpikeOptions options, EvidenceWriter writer)
    {
        var engine = new Engine(options);
        var shell = new ContainerShell(options);
        shell.Write();

        var workspace = new Workspace(options.Workspace);
        var sink = new RecordingSink();
        ToolContext ctx = Tooling.Context(options, sink);
        var local = new BashTool(workspace);
        var container = new BashTool(workspace, shell.Resolver());

        writer.Section("tool call cost, same product code, only the shell seam moved");
        var rows = new List<(string Name, double[] Samples)>
        {
            (
                "bash tool, local shell",
                await SamplesAsync(
                    options.Runs,
                    () => local.ExecuteAsync(Tooling.BashArgs("true"), ctx, CancellationToken.None)
                )
            ),
            (
                "bash tool, container through the shim",
                await SamplesAsync(
                    options.Runs,
                    () =>
                        container.ExecuteAsync(
                            Tooling.BashArgs("true"),
                            ctx,
                            CancellationToken.None
                        )
                )
            ),
            (
                "engine exec: client + /bin/bash -c true",
                Timed(options.Runs, () => engine.Exec(options.Container, "true"))
            ),
            (
                "engine exec: client + true (no shell)",
                Timed(
                    options.Runs,
                    () => Proc.Run(options.Engine, ["exec", options.Container, "true"])
                )
            ),
            (
                "engine exec: client + /bin/bash -c true, -w workspace",
                Timed(
                    options.Runs,
                    () =>
                        Proc.Run(
                            options.Engine,
                            [
                                "exec",
                                "-i",
                                "-w",
                                options.Workspace,
                                options.Container,
                                "/bin/bash",
                                "-c",
                                "true",
                            ]
                        )
                )
            ),
        };
        writer.Line("| row | n | min ms | median ms | p95 ms | max ms |");
        writer.Line("| --- | --- | --- | --- | --- | --- |");
        foreach ((string name, double[] samples) in rows)
        {
            writer.Line(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {name} | {samples.Length} | {Stat(samples, 0.0)} | {Stat(samples, 0.5)} | {Stat(samples, 0.95)} | {Stat(samples, 1.0)} |"
                )
            );
        }

        double localMedian = Quantile(rows[0].Samples, 0.5);
        double containerMedian = Quantile(rows[1].Samples, 0.5);
        writer.Key(
            "per-call overhead of the container environment",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{containerMedian - localMedian:0.0} ms on this host (median), {containerMedian / Math.Max(localMedian, 0.001):0.0}x the local call"
            )
        );

        writer.Section("cold container: create + start, then remove");
        double[] cold = Timed(Math.Min(options.Runs, 5), () => RunColdContainer(engine, options));
        writer.Line(
            string.Create(
                CultureInfo.InvariantCulture,
                $"create+start+run -d ({cold.Length} samples): min {Stat(cold, 0.0)} ms, median {Stat(cold, 0.5)} ms, max {Stat(cold, 1.0)} ms"
            )
        );

        writer.Section("stop and start of an existing container");
        double[] restart = Timed(Math.Min(options.Runs, 3), () => StopStart(engine, options));
        writer.Line(
            string.Create(
                CultureInfo.InvariantCulture,
                $"stop+start ({restart.Length} samples): min {Stat(restart, 0.0)} ms, median {Stat(restart, 0.5)} ms, max {Stat(restart, 1.0)} ms"
            )
        );

        return writer.Finish();
    }

    private static ProcResult RunColdContainer(Engine engine, SpikeOptions options)
    {
        string name = "s7-cold-" + Guid.NewGuid().ToString("N")[..8];
        ProcResult result = Proc.Run(
            options.Engine,
            [
                "run",
                "-d",
                "--rm",
                "--name",
                name,
                "-v",
                $"{options.Workspace}:{options.Workspace}",
                "-w",
                options.Workspace,
                options.Image,
                "sleep",
                "30",
            ],
            workingDirectory: options.Workspace,
            timeout: TimeSpan.FromMinutes(2)
        );
        Proc.Run(options.Engine, ["rm", "-f", name]);
        return result;
    }

    private static ProcResult StopStart(Engine engine, SpikeOptions options)
    {
        string name = "s7-restart-fixed";
        Proc.Run(options.Engine, ["rm", "-f", name]);
        ProcResult created = Proc.Run(
            options.Engine,
            [
                "run",
                "-d",
                "--name",
                name,
                "-v",
                $"{options.Workspace}:{options.Workspace}",
                "-w",
                options.Workspace,
                options.Image,
                "sleep",
                "600",
            ],
            workingDirectory: options.Workspace,
            timeout: TimeSpan.FromMinutes(2)
        );
        if (!created.Ok)
        {
            return created;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Proc.Run(
            options.Engine,
            ["stop", name],
            workingDirectory: options.Workspace,
            timeout: TimeSpan.FromMinutes(2)
        );
        ProcResult started = Proc.Run(
            options.Engine,
            ["start", name],
            workingDirectory: options.Workspace,
            timeout: TimeSpan.FromMinutes(2)
        );
        stopwatch.Stop();
        Proc.Run(options.Engine, ["rm", "-f", name]);
        return started with { Millis = stopwatch.Elapsed.TotalMilliseconds };
    }

    private static double[] Timed(int runs, Func<ProcResult> action)
    {
        var samples = new List<double>(runs);
        for (int index = 0; index < runs; index++)
        {
            samples.Add(action().Millis);
        }

        return [.. samples];
    }

    private static async Task<double[]> SamplesAsync(int runs, Func<Task<ToolResult>> action)
    {
        var samples = new List<double>(runs);
        for (int index = 0; index < runs; index++)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await action();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return [.. samples];
    }

    /// <summary>Min (<paramref name="quantile"/> 0), median (0.5), p95 or max (1), invariant.</summary>
    private static string Stat(double[] samples, double quantile) =>
        FileToolEvidence.Invariant($"{Quantile(samples, quantile):0.0}");

    private static double Quantile(double[] samples, double quantile)
    {
        if (samples.Length == 0)
        {
            return double.NaN;
        }

        double[] sorted = [.. samples.Order()];
        int index = (int)Math.Round(quantile * (sorted.Length - 1), MidpointRounding.AwayFromZero);
        return sorted[index];
    }
}
