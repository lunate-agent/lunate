using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Lunate.Coding.Tests;

/// <summary>
/// The evidence a timed-out wait collects about the machine it was waiting on: the thread pool's
/// state, and the process table for this test process and its descendants — state, wait channel and,
/// where the platform exposes them, the open descriptors.
/// </summary>
/// <remarks>
/// A wait that burns its whole budget is usually a process further down this list that is blocked on
/// something the wait cannot see: a child waiting on a pipe, a <c>setsid</c> child that outlived its
/// redirect, or a pipe whose write end an unrelated process still holds (#660, #699). The collection
/// is bounded by <see cref="WaitBudgets.Diagnosis"/> and is best-effort by construction: every failure
/// is reported as text, because the diagnosis is appended to an exception that is thrown either way
/// and must never replace it.
/// </remarks>
internal static class WaitDiagnosis
{
    /// <summary>How many descriptors of one process the message carries before it says "…".</summary>
    private const int DescriptorCap = 40;

    /// <summary>The thread pool and the process tree, as failure-message lines.</summary>
    public static async Task<string> DescribeAsync(CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        text.Append(
            FormattableString.Invariant(
                $"threadPool: threads={ThreadPool.ThreadCount} pending={ThreadPool.PendingWorkItemCount} completed={ThreadPool.CompletedWorkItemCount}"
            )
        );
        text.AppendLine().Append("processTree:");
        foreach (string line in await LinesAsync(cancellationToken).ConfigureAwait(false))
        {
            text.AppendLine().Append("  ").Append(line);
        }

        return text.ToString();
    }

    private static async Task<IReadOnlyList<string>> LinesAsync(CancellationToken cancellationToken)
    {
        (string table, string? failure) read = OperatingSystem.IsWindows()
            ? await ReadAsync(
                    "powershell",
                    [
                        "-NoProfile",
                        "-NonInteractive",
                        "-Command",
                        "Get-CimInstance Win32_Process | ForEach-Object { \"$($_.ProcessId) $($_.ParentProcessId) $($_.Name)\" }",
                    ],
                    cancellationToken
                )
                .ConfigureAwait(false)
            : await ReadAsync(
                    "/bin/ps",
                    ["-axo", "pid=,ppid=,pgid=,stat=,wchan=,comm="],
                    cancellationToken
                )
                .ConfigureAwait(false);

        if (read.failure is { } reason)
        {
            return [$"unavailable: {reason}"];
        }

        return DescribeTable(
            read.table,
            Environment.ProcessId,
            OperatingSystem.IsWindows() ? "Win32_Process" : "/bin/ps"
        );
    }

    /// <summary>
    /// The lines for one process table: the source line, one per descendant of <paramref name="self"/>
    /// (root first, descriptors where they can be read), then a note when the platform has no
    /// descriptors to read. Pure, so the parser and the tree walk are pinned without a live <c>ps</c>.
    /// </summary>
    internal static IReadOnlyList<string> DescribeTable(string table, int self, string source)
    {
        IReadOnlyList<ProcessRow> rows = Parse(table);
        if (rows.Count == 0)
        {
            return ["unavailable: the process table was empty"];
        }

        var lines = new List<string>();
        IReadOnlyList<ProcessRow> tree = Descendants(rows, self);
        lines.Add(
            FormattableString.Invariant(
                $"source={source} self={self} tree={tree.Count} processes={rows.Count}"
            )
        );
        foreach (ProcessRow row in tree)
        {
            string described =
                row.Describe() + (row.Pid == self ? " (this test process)" : string.Empty);
            string descriptors = Descriptors(row.Pid);
            lines.Add(descriptors.Length == 0 ? described : described + " " + descriptors);
        }

        if (!OperatingSystem.IsLinux())
        {
            lines.Add(
                OperatingSystem.IsWindows()
                    ? "descriptors: not collected on this platform (the Windows tree carries pid, parent and name only)"
                    : "descriptors: not collected on this platform (per-process descriptors need /proc)"
            );
        }

        return lines;
    }

    /// <summary>The <c>/proc/&lt;pid&gt;/fd</c> links of one process, capped; empty off Linux.</summary>
    private static string Descriptors(int pid)
    {
        if (!OperatingSystem.IsLinux())
        {
            return string.Empty;
        }

        string directory = string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/fd");
        var links = new List<string>();
        try
        {
            foreach (
                string path in Directory
                    .EnumerateFileSystemEntries(directory)
                    .Order(StringComparer.Ordinal)
            )
            {
                if (links.Count == DescriptorCap)
                {
                    links.Add("…");
                    break;
                }

                try
                {
                    string target =
                        File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName ?? "?";
                    links.Add(FormattableString.Invariant($"{Path.GetFileName(path)}->{target}"));
                }
                catch (Exception exception)
                    when (exception is IOException or UnauthorizedAccessException)
                {
                    // The process closed the descriptor between the listing and the read.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "fds=<unreadable>";
        }

        return "fds=" + string.Join(",", links);
    }

    /// <summary>Runs one diagnostic command, bounded: a hang here is reported, never thrown.</summary>
    private static async Task<(string Table, string? Failure)> ReadAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            process = Process.Start(startInfo);
            if (process is null)
            {
                return (string.Empty, $"could not start {fileName}");
            }

            string table = await process
                .StandardOutput.ReadToEndAsync(cancellationToken)
                .WaitAsync(WaitBudgets.Diagnosis, cancellationToken)
                .ConfigureAwait(false);
            return (table, null);
        }
        catch (Exception exception)
            when (exception
                    is Win32Exception
                        or InvalidOperationException
                        or TimeoutException
                        or OperationCanceledException
            )
        {
            if (process is not null)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception killFailure)
                    when (killFailure is InvalidOperationException or Win32Exception)
                {
                    // Already gone.
                }
            }

            return (
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{fileName} did not answer within {WaitBudgets.Diagnosis.TotalSeconds:0} seconds ({exception.GetType().Name}: {exception.Message}); the tree could not be collected"
                )
            );
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>One process-table row; the columns the issue asks for, kept as text.</summary>
    internal sealed record ProcessRow(
        int Pid,
        int Parent,
        string Group,
        string State,
        string WaitChannel,
        string Command
    )
    {
        public string Describe() =>
            FormattableString.Invariant(
                $"pid={Pid} ppid={Parent} pgid={Group} stat={State} wchan={WaitChannel} comm={Command}"
            );
    }

    /// <summary>
    /// Reads both table shapes: <c>ps</c>'s six columns (<c>comm</c> last, so a command may carry
    /// spaces) and the PowerShell walk's three. A row that does not start with a pid is skipped.
    /// </summary>
    private static IReadOnlyList<ProcessRow> Parse(string table)
    {
        var rows = new List<ProcessRow>();
        foreach (string raw in table.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(
                (char[]?)null,
                6,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            if (
                parts.Length < 2
                || !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int pid
                )
                || !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parent
                )
            )
            {
                continue;
            }

            rows.Add(
                new ProcessRow(
                    pid,
                    parent,
                    parts.Length > 2 ? parts[2] : "-",
                    parts.Length > 3 ? parts[3] : "-",
                    parts.Length > 4 ? parts[4] : "-",
                    parts.Length > 5 ? parts[5] : "?"
                )
            );
        }

        return rows;
    }

    /// <summary>This process and its descendants, root first, in pid order.</summary>
    private static IReadOnlyList<ProcessRow> Descendants(IReadOnlyList<ProcessRow> rows, int root)
    {
        var children = new Dictionary<int, List<ProcessRow>>();
        ProcessRow? self = null;
        foreach (ProcessRow row in rows)
        {
            if (row.Pid == root)
            {
                self = row;
            }

            if (row.Pid == row.Parent)
            {
                continue;
            }

            if (!children.TryGetValue(row.Parent, out List<ProcessRow>? list))
            {
                children[row.Parent] = list = [];
            }

            list.Add(row);
        }

        var ordered = new List<ProcessRow>();
        var visited = new HashSet<int>();

        void Collect(ProcessRow row)
        {
            if (!visited.Add(row.Pid))
            {
                return;
            }

            ordered.Add(row);
            if (!children.TryGetValue(row.Pid, out List<ProcessRow>? list))
            {
                return;
            }

            foreach (ProcessRow child in list.OrderBy(entry => entry.Pid))
            {
                Collect(child);
            }
        }

        if (self is { } own)
        {
            Collect(own);
        }

        return ordered;
    }
}
