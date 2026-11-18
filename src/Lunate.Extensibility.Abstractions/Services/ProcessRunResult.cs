namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// The outcome of one process run. Output is decoded as UTF-8 and capped per stream: reading
/// continues past the cap so the child never blocks, but the text stops there and
/// <see cref="StdoutCapped"/>/<see cref="StderrCapped"/> report it — the caller renders any marker.
/// </summary>
/// <param name="ExitCode">The exit code; null when the run timed out and the tree was killed.</param>
/// <param name="Stdout">The captured standard output (capped).</param>
/// <param name="Stderr">The captured standard error (capped).</param>
/// <param name="TimedOut">True when the deadline expired and the tree was killed.</param>
public sealed record ProcessRunResult(int? ExitCode, string Stdout, string Stderr, bool TimedOut)
{
    /// <summary>True when standard output hit the cap and its text was truncated.</summary>
    public bool StdoutCapped { get; init; }

    /// <summary>True when standard error hit the cap and its text was truncated.</summary>
    public bool StderrCapped { get; init; }

    /// <summary>True when either stream hit the cap and its text was truncated.</summary>
    public bool OutputCapped => StdoutCapped || StderrCapped;
}
