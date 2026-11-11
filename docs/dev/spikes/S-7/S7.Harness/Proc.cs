using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace S7;

/// <summary>One finished child process: its argv, streams, exit code and wall time.</summary>
internal sealed record ProcResult(
    string FileName,
    IReadOnlyList<string> Arguments,
    int ExitCode,
    string Stdout,
    string Stderr,
    double Millis
)
{
    internal bool Ok => ExitCode == 0;

    /// <summary>The argv as one line, for the evidence files.</summary>
    internal string CommandLine => FileName + " " + string.Join(' ', Arguments);

    internal string StdoutTrimmed => Stdout.TrimEnd();
}

/// <summary>
/// The spike's own child-process runner. The product's <c>ProcessRunner</c> is internal to
/// <c>Lunate.Coding</c>; this copies only the parts the evidence needs (redirected streams, closed
/// stdin, a timeout with a tree kill) so the container CLI calls can be measured the same way.
/// </summary>
internal static class Proc
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    internal static ProcResult Run(string fileName, params string[] arguments) =>
        Run(fileName, arguments, workingDirectory: null, timeout: null);

    internal static ProcResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        TimeSpan? timeout
    )
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? ".",
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        using var process =
            Process.Start(info)
            ?? throw new InvalidOperationException($"could not start {fileName}");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();

        if (!process.WaitForExit((int)(timeout ?? DefaultTimeout).TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already gone between the timeout and the kill; the result is still a timeout.
            }

            process.WaitForExit(5_000);
            return new ProcResult(
                fileName,
                arguments,
                -1,
                stdout.GetAwaiter().GetResult(),
                stderr.GetAwaiter().GetResult(),
                stopwatch.Elapsed.TotalMilliseconds
            );
        }

        return new ProcResult(
            fileName,
            arguments,
            process.ExitCode,
            stdout.GetAwaiter().GetResult(),
            stderr.GetAwaiter().GetResult(),
            stopwatch.Elapsed.TotalMilliseconds
        );
    }
}

/// <summary>Milliseconds, always written culture-invariant.</summary>
internal static class Num
{
    internal static string Ms(double milliseconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{milliseconds:0.0}");

    internal static string One(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:0.0}");
}
