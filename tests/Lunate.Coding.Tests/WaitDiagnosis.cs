using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Lunate.Coding.Tests;

/// <summary>
/// The evidence a timed-out wait collects about the machine it was waiting on: the thread pool's
/// state, and the process table for this test process and its descendants (rendered by
/// <see cref="ProcessTable"/>).
/// </summary>
/// <remarks>
/// A wait that burns its whole budget is usually a process further down the tree that is blocked on
/// something the wait cannot see: a child waiting on a pipe, a <c>setsid</c> child that outlived its
/// redirect, or a pipe whose write end an unrelated process still holds (#660, #699). The collection
/// is bounded by <see cref="WaitBudgets.Diagnosis"/> and is best-effort by construction: every failure
/// is reported as text, because the diagnosis is appended to an exception that is thrown either way
/// and must never replace it.
/// </remarks>
internal static class WaitDiagnosis
{
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

        return ProcessTable.Describe(
            read.table,
            Environment.ProcessId,
            OperatingSystem.IsWindows() ? "Win32_Process" : "/bin/ps"
        );
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
}
