using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

/// <summary>
/// The <c>core/process</c> facade: implements <see cref="IProcessRunner"/> over the active
/// execution environment. The extension/MCP and restore/Roslyn paths already consume this runner,
/// so they share the tool-command seam without a public contract or registration change. Host
/// support that must stay local uses <see cref="Local"/>, which names the local environment
/// explicitly.
/// </summary>
internal sealed class ProcessRunner : IProcessRunner
{
    private readonly IExecutionEnvironment _environment;

    /// <summary>The seam-selected environment; local when nothing else is configured.</summary>
    public ProcessRunner()
        : this(LocalExecutionEnvironment.Instance) { }

    internal ProcessRunner(IExecutionEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>Host support (change scan, clipboard) that must not move with a future environment.</summary>
    internal static ProcessRunner Local { get; } = new(LocalExecutionEnvironment.Instance);

    internal string Description => _environment.Description;

    public Task<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken
    ) => _environment.RunAsync(request, cancellationToken);

    public Task<IProcessSession> StartSessionAsync(
        ProcessSessionRequest request,
        CancellationToken cancellationToken
    ) => _environment.StartSessionAsync(request, cancellationToken);

    /// <summary>
    /// Kills a POSIX process tree by enumerating descendants with <c>ps</c> and sending
    /// SIGKILL to each child before its parent.
    /// </summary>
    /// <remarks>
    /// Deliberately avoids <see cref="Process.Kill(bool)"/> with <c>entireProcessTree: true</c>:
    /// its Unix implementation SIGSTOPs the whole tree before killing it, which was observed to
    /// wedge macOS CI VMs — the runner agent stops responding, no logs or step timeouts fire,
    /// and the job dies with "the hosted runner lost communication with the server". A minimal
    /// repro (spawn <c>sh -c 'exec sleep 300' &amp; sleep 300</c>; call the framework tree kill)
    /// wedges the VM, while this enumeration path completes in milliseconds. The Windows
    /// implementation (job objects) is unaffected and stays on the framework path.
    /// </remarks>
    internal static void KillPosixTree(int rootPid)
    {
        IReadOnlyList<int> targets;
        try
        {
            targets = EnumerateDescendants(ReadProcessTable(), rootPid);
        }
        catch (Exception exception)
            when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            // Enumeration failed (e.g. ps unavailable); degrade to killing the root only.
            targets = [rootPid];
        }

        foreach (int pid in targets)
        {
            try
            {
                Process.GetProcessById(pid).Kill();
            }
            catch (Exception exception)
                when (exception is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Already exited or not ours to kill; best effort by design.
            }
        }
    }

    /// <summary>Collects the root's descendants (children before parents), root last.</summary>
    internal static IReadOnlyList<int> EnumerateDescendants(string processTable, int rootPid)
    {
        var parentOf = new Dictionary<int, int>();
        foreach (string line in processTable.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (
                parts.Length == 2
                && int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int pid
                )
                && int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parent
                )
                && pid > 0
                && pid != parent
            )
            {
                parentOf[pid] = parent;
            }
        }

        var ordered = new List<int>();
        var visited = new HashSet<int>();

        void Collect(int pid)
        {
            if (!visited.Add(pid))
            {
                return;
            }

            foreach (
                int child in parentOf
                    .Where(entry => entry.Value == pid)
                    .Select(entry => entry.Key)
                    .Order()
            )
            {
                Collect(child);
            }

            ordered.Add(pid);
        }

        Collect(rootPid);
        return ordered;
    }

    private static string ReadProcessTable()
    {
        using var ps = Process.Start(
            new ProcessStartInfo("/bin/ps")
            {
                RedirectStandardOutput = true,
                ArgumentList = { "-axo", "pid=,ppid=" },
            }
        )!;
        string output = ps.StandardOutput.ReadToEnd();
        ps.WaitForExit();
        return output;
    }
}
