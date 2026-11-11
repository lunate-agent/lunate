using S7;

// S-7 spike harness (#625): "execution environments — where do Lunate's tools run?".
// Modes: env, file-tool, bash-tool, startup, cleanup. See docs/spikes/S-7/README.md.
SpikeOptions options = SpikeOptions.Parse(args);
var evidence = new EvidenceWriter(Console.Out);

int exit = options.Mode switch
{
    "env" => EnvironmentEvidence.Run(options, evidence),
    "file-tool" => await FileToolEvidence.RunAsync(options, evidence),
    "bash-tool" => await BashToolEvidence.RunAsync(options, evidence),
    "startup" => await StartupEvidence.RunAsync(options, evidence),
    "cleanup" => Cleanup(options, evidence),
    _ => Usage(evidence),
};

return exit;

static int Usage(EvidenceWriter evidence)
{
    evidence.Line("usage: S7.Harness <env|file-tool|bash-tool|startup|cleanup> [options]");
    evidence.Line("  --engine <podman|docker>   container CLI (default: $S7_ENGINE or podman)");
    evidence.Line("  --container <name>         running container (default: $S7_CONTAINER)");
    evidence.Line("  --workspace <path>         the workspace to mount (default: cwd)");
    evidence.Line("  --image <ref>              image for the cold-start rows");
    evidence.Line("  --runs <n>                  timing samples per row (default: 15)");
    return 2;
}

static int Cleanup(SpikeOptions options, EvidenceWriter evidence)
{
    if (Directory.Exists(options.EvidenceDirectory))
    {
        Directory.Delete(options.EvidenceDirectory, recursive: true);
    }

    foreach (string probe in new[] { "s7-scan-probe.txt", "s7-probe.txt" })
    {
        string path = Path.Combine(options.Workspace, probe);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    string shim = Path.Combine(Path.GetTempPath(), "s7-container-shell.sh");
    if (File.Exists(shim))
    {
        File.Delete(shim);
    }

    evidence.Line("removed the workspace evidence directory and the shim");
    return evidence.Finish();
}
