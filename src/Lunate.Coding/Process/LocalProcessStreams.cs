using System.Text;

namespace Lunate.Coding;

/// <summary>
/// The local runner's stream pumps: bounded UTF-8 reads, the optional stdin line pump and the
/// drain that keeps a killed child's already-written output. They are local mechanics, not part of
/// the execution-environment contract.
/// </summary>
internal static class LocalProcessStreams
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Serves every stdout line to the handler in order and writes each non-null answer back to
    /// the child's stdin as one flushed line. Delivery is never capped; only the accumulated text
    /// is. A closed stdout ends the pump. A closed stdin only ends the answers: a child that exits
    /// (or stops reading) while an answer is in flight still has every line it wrote delivered,
    /// because its output is the run's result and the pipe it closed is not evidence about it.
    /// </summary>
    public static async Task<(string Text, bool Capped)> PumpLinesAsync(
        StreamReader reader,
        StreamWriter stdin,
        Func<string, CancellationToken, Task<string?>> handler,
        int cap,
        CancellationToken ct
    )
    {
        var builder = new StringBuilder();
        var bytes = 0;
        var capped = false;
        var stdinOpen = true;
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception) when (IsPipeClosed(exception))
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            if (!capped)
            {
                int lineBytes = Utf8.GetByteCount(line) + 1;
                if (bytes + lineBytes <= cap)
                {
                    bytes += lineBytes;
                    builder.Append(line).Append('\n');
                }
                else
                {
                    capped = true;
                }
            }

            string? response;
            try
            {
                response = await handler(line, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (IsPipeClosed(exception))
            {
                break;
            }

            if (response is null || !stdinOpen)
            {
                continue;
            }

            try
            {
                await stdin.WriteAsync(response.AsMemory()).ConfigureAwait(false);
                await stdin.WriteAsync("\n".AsMemory()).ConfigureAwait(false);
                await stdin.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (IsPipeClosed(exception))
            {
                // The child closed its stdin — usually because it exited while the answer was in
                // flight (two writes: the answer, then the newline). Keep draining stdout: the
                // lines it wrote before exiting are the run's output, and abandoning the pump
                // here loses them, which is indistinguishable from a child that never answered.
                stdinOpen = false;
            }
        }

        return (builder.ToString(), capped);
    }

    /// <summary>
    /// Reads one stream into bounded UTF-8 text. Bytes beyond the cap are drained and discarded,
    /// so the child never blocks on a full pipe; the decoded text stops at the cap.
    /// </summary>
    public static async Task<(string Text, bool Capped)> ReadBoundedAsync(
        Stream stream,
        int cap,
        CancellationToken ct
    )
    {
        var builder = new StringBuilder();
        Decoder decoder = Utf8.GetDecoder();
        var buffer = new byte[8192];
        var chars = new char[8192];
        var bytesRead = 0;
        var capped = false;
        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsPipeClosed(exception))
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            int remaining = cap - bytesRead;
            int use = Math.Min(read, Math.Max(remaining, 0));
            if (use < read)
            {
                capped = true;
            }

            if (use > 0)
            {
                bytesRead += use;
                int count = decoder.GetChars(buffer, 0, use, chars, 0, flush: false);
                builder.Append(chars, 0, count);
            }
        }

        int tail = decoder.GetChars([], 0, 0, chars, 0, flush: true);
        builder.Append(chars, 0, tail);
        return (builder.ToString(), capped);
    }

    public static bool IsPipeClosed(Exception exception) =>
        exception is IOException or ObjectDisposedException or InvalidOperationException;

    public static async Task<(
        string Stdout,
        string Stderr,
        bool StdoutCapped,
        bool StderrCapped
    )> DrainAsync(
        Task<(string Text, bool Capped)> stdoutTask,
        Task<(string Text, bool Capped)> stderrTask
    )
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask)
                .WaitAsync(TimeSpan.FromSeconds(10))
                .ConfigureAwait(false);
        }
        catch (TimeoutException) { }

        bool stdoutDone = stdoutTask.Status == TaskStatus.RanToCompletion;
        bool stderrDone = stderrTask.Status == TaskStatus.RanToCompletion;
        return (
            stdoutDone ? stdoutTask.Result.Text : string.Empty,
            stderrDone ? stderrTask.Result.Text : string.Empty,
            stdoutDone && stdoutTask.Result.Capped,
            stderrDone && stderrTask.Result.Capped
        );
    }
}
