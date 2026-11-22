namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// One process to run. <see cref="Environment"/> is exactly what the child receives: null inherits
/// the runner's own environment; a non-null dictionary replaces it entirely (extensions should pass
/// an explicit allow-list so Lunate's environment — provider keys included — cannot leak).
/// </summary>
/// <param name="FileName">The executable to start.</param>
/// <param name="Arguments">The argument list, passed verbatim (no shell interpolation).</param>
/// <param name="WorkingDirectory">The child's working directory.</param>
/// <param name="Environment">The child's environment; null inherits, non-null replaces.</param>
/// <param name="Timeout">The run's deadline; null runs without one.</param>
/// <param name="OutputCapBytes">The per-stream output cap; null uses the runner's default.</param>
public sealed record ProcessRunRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string>? Environment = null,
    TimeSpan? Timeout = null,
    int? OutputCapBytes = null
)
{
    /// <summary>
    /// Values applied on top of <see cref="Environment"/> — or on top of an inherited environment
    /// when that is null — after the replacement above, so a caller can add a marker (the bash tool
    /// adds <c>LUNATE_SESSION</c>) without restating the child's whole environment and without
    /// losing it. A name in both wins here.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ExtraEnvironment { get; init; }

    /// <summary>
    /// Serves interactive children: when set, every stdout line is handed to it, in order, while
    /// the process runs, and a non-null return is written back to the child's stdin as one flushed
    /// line. Delivery is never capped; <see cref="ProcessRunResult.OutputCapped"/> only reports the
    /// accumulated text. Null (the default) closes the child's stdin — the batch mode the bash tool
    /// uses.
    /// </summary>
    public Func<string, CancellationToken, Task<string?>>? LineHandler { get; init; }
}
