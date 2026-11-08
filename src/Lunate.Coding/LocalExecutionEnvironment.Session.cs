using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

/// <summary>
/// The interactive half of local execution: one long-lived child with line-oriented
/// stdin/stdout/stderr and a tree kill that reuses the run path's children-first walk. The remote
/// extension host consumes it through <see cref="IProcessRunner.StartSessionAsync"/>.
/// </summary>
internal sealed partial class LocalExecutionEnvironment
{
    public async Task<IProcessSession> StartSessionAsync(
        ProcessSessionRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentNullException.ThrowIfNull(request.Arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);
        ArgumentNullException.ThrowIfNull(request.Environment);
        cancellationToken.ThrowIfCancellationRequested();

        // Reassign to the mapped request so this spawn site still names the complete environment
        // it hands the child, while a future environment can translate the command first.
        request = MapSessionRequest(request);
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The environment is exactly what the caller passes: no inherit mode exists, so a missing
        // variable is absent, not inherited.
        startInfo.Environment.Clear();
        foreach ((string name, string value) in request.Environment)
        {
            startInfo.Environment[name] = value;
        }

        Process process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("the process did not start");
        var session = new SessionProcess(process, request.MaxLineBytes);
        try
        {
            await session.ProbeAsync(cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private ProcessSessionRequest MapSessionRequest(ProcessSessionRequest request)
    {
        ExecutionCommand mapped = Map(
            new ExecutionCommand(
                request.FileName,
                request.Arguments,
                request.WorkingDirectory,
                request.Environment
            )
        );
        return request with
        {
            FileName = mapped.Executable,
            Arguments = mapped.Arguments,
            WorkingDirectory = mapped.WorkingDirectory,
            Environment = mapped.Environment ?? request.Environment,
        };
    }

    /// <summary>
    /// One started child: bounded line readers over both streams, a flushed stdin writer and the
    /// shared tree kill. Disposal kills a running child, so the caller always owns shutdown.
    /// </summary>
    private sealed class SessionProcess(Process process, int? maxLineBytes) : IProcessSession
    {
        private readonly SemaphoreSlim _stdinGate = new(1, 1);
        private readonly BoundedLineReader _stdout = new(
            process.StandardOutput.BaseStream,
            maxLineBytes
        );
        private readonly BoundedLineReader _stderr = new(
            process.StandardError.BaseStream,
            maxLineBytes
        );
        private bool _disposed;

        /// <summary>
        /// Confirms the child survived the start window; a process that died between
        /// <c>Process.Start</c> and here still yields a usable session whose streams report EOF, so
        /// this only observes the declared cancellation.
        /// </summary>
        public Task ProbeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public bool HasExited
        {
            get
            {
                try
                {
                    return process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // The process object was disposed (our own DisposeAsync ran): it was killed.
                    return true;
                }
            }
        }

        public int? ExitCode => HasExited ? process.ExitCode : null;

        public ValueTask<string?> ReadOutputLineAsync(CancellationToken cancellationToken) =>
            _stdout.ReadLineAsync(cancellationToken);

        public ValueTask<string?> ReadErrorLineAsync(CancellationToken cancellationToken) =>
            _stderr.ReadLineAsync(cancellationToken);

        public async ValueTask WriteInputLineAsync(string line, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(line);
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _stdinGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await process
                    .StandardInput.WriteAsync(line.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                await process
                    .StandardInput.WriteAsync("\n".AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (LocalProcessStreams.IsPipeClosed(exception))
            {
                throw new InvalidOperationException(
                    "the child's stdin is closed; the process has exited.",
                    exception
                );
            }
            finally
            {
                _stdinGate.Release();
            }
        }

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode;
        }

        public async ValueTask KillAsync()
        {
            if (_disposed || HasExited)
            {
                return;
            }

            await KillTreeAsync(process).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!HasExited)
            {
                await KillTreeAsync(process).ConfigureAwait(false);
            }

            try
            {
                process.StandardInput.Dispose();
            }
            catch (Exception exception) when (LocalProcessStreams.IsPipeClosed(exception)) { }

            process.Dispose();
            _stdinGate.Dispose();
        }
    }

    private static async Task KillTreeAsync(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception)
                when (exception
                        is InvalidOperationException
                            or Win32Exception
                            or NotSupportedException
                ) { }
        }
        else
        {
            // System.Diagnostics' entireProcessTree path SIGSTOPs the whole tree first and
            // wedges macOS CI VMs (runner loses communication; repro on the T-15 PR).
            // Enumerate with ps and SIGKILL children first instead — see KillPosixTree.
            ProcessRunner.KillPosixTree(process.Id);
        }

        try
        {
            await process
                .WaitForExitAsync(CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(false);
        }
        catch (TimeoutException) { }
    }
}
