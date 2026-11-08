using System.Globalization;

namespace Lunate.Coding.Tests;

/// <summary>
/// One process table as failure-message lines: the source line, one line per descendant of the test
/// process (root first, with its state, wait channel and — where the platform exposes them — its open
/// descriptors), then a note when the platform has no descriptors to read.
/// </summary>
/// <remarks>
/// The columns are the ones #660 asked for: <c>ps -axo pid=,ppid=,pgid=,stat=,wchan=,comm=</c> on
/// POSIX, a three-column <c>Win32_Process</c> walk on Windows. A child blocked in a read shows up as a
/// non-<c>S</c> state or a wait channel, and on Linux a pipe whose write end a bystander still holds
/// shows up as a descriptor the two processes share — the two things neither the wait nor <c>ps</c>
/// alone can say.
/// </remarks>
internal static class ProcessTable
{
    /// <summary>How many descriptors of one process the message carries before it says "…".</summary>
    private const int DescriptorCap = 40;

    /// <summary>
    /// The lines for one process table: the source line, one per descendant of <paramref name="self"/>
    /// (root first, descriptors where they can be read), then a note when the platform has no
    /// descriptors to read. Pure, so the parser and the tree walk are pinned without a live <c>ps</c>.
    /// </summary>
    internal static IReadOnlyList<string> Describe(string table, int self, string source)
    {
        IReadOnlyList<Row> rows = Parse(table);
        if (rows.Count == 0)
        {
            return ["unavailable: the process table was empty"];
        }

        var lines = new List<string>();
        IReadOnlyList<Row> tree = Descendants(rows, self);
        lines.Add(
            FormattableString.Invariant(
                $"source={source} self={self} tree={tree.Count} processes={rows.Count}"
            )
        );
        foreach (Row row in tree)
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

    /// <summary>One process-table row; the columns the issue asks for, kept as text.</summary>
    internal sealed record Row(
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
    private static IReadOnlyList<Row> Parse(string table)
    {
        var rows = new List<Row>();
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
                new Row(
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
    private static IReadOnlyList<Row> Descendants(IReadOnlyList<Row> rows, int root)
    {
        var children = new Dictionary<int, List<Row>>();
        Row? self = null;
        foreach (Row row in rows)
        {
            if (row.Pid == root)
            {
                self = row;
            }

            if (row.Pid == row.Parent)
            {
                continue;
            }

            if (!children.TryGetValue(row.Parent, out List<Row>? list))
            {
                children[row.Parent] = list = [];
            }

            list.Add(row);
        }

        var ordered = new List<Row>();
        var visited = new HashSet<int>();

        void Collect(Row row)
        {
            if (!visited.Add(row.Pid))
            {
                return;
            }

            ordered.Add(row);
            if (!children.TryGetValue(row.Pid, out List<Row>? list))
            {
                return;
            }

            foreach (Row child in list.OrderBy(entry => entry.Pid))
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
