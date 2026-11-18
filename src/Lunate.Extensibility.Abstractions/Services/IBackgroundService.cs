namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// A session-scoped background service: an LSP server, watcher or connection. The host starts it
/// with <c>SessionStarted</c> semantics and stops it idempotently on <c>SessionEnding</c>. A
/// failure is reported and contained: it never crashes the run.
/// </summary>
public interface IBackgroundService
{
    ValueTask StartAsync(CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
