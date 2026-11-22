using System.Globalization;

namespace Lunate.Coding;

/// <summary>
/// One session's background bash jobs. The three bash tools share it, ids are unique within the
/// session, and closing it kills every tree the session still owns. It is deliberately not a
/// process-wide singleton: an ACP host can serve more than one session, and one session must never
/// end another's work.
/// </summary>
internal sealed class BashJobRegistry
{
    private readonly Dictionary<string, BashJob> _jobs = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private long _sequence;
    private bool _closed;

    public async Task<BashJob> StartAsync(
        string command,
        Func<string, Task<StartedBackgroundProcess>> start,
        TimeSpan timeout
    )
    {
        ArgumentNullException.ThrowIfNull(start);
        string id;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            id = string.Create(
                CultureInfo.InvariantCulture,
                $"job-{Interlocked.Increment(ref _sequence)}"
            );
        }

        StartedBackgroundProcess started = await start(id).ConfigureAwait(false);
        BashJob job = BashJob.Start(id, command, started, timeout);
        bool closed;
        lock (_gate)
        {
            closed = _closed;
            if (!closed)
            {
                _jobs.Add(id, job);
            }
        }

        if (closed)
        {
            await job.KillAsync().ConfigureAwait(false);
            throw new InvalidOperationException("the session is ending; the job was not started");
        }

        return job;
    }

    public BashJob? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            return _jobs.GetValueOrDefault(id);
        }
    }

    /// <summary>Kills every job this registry owns and waits for each tree to settle.</summary>
    public async Task KillAllAsync()
    {
        BashJob[] jobs;
        lock (_gate)
        {
            if (_closed)
            {
                jobs = [];
            }
            else
            {
                _closed = true;
                jobs = [.. _jobs.Values];
                _jobs.Clear();
            }
        }

        foreach (BashJob job in jobs)
        {
            await job.KillAsync().ConfigureAwait(false);
        }
    }
}
