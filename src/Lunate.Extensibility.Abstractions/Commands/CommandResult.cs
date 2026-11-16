namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// The outcome of an invocation: at most one user-facing message. <see cref="IsError"/> selects the
/// error notice level; a source may act through its own host callback and return
/// <see cref="None"/> when it has nothing to say.
/// </summary>
public sealed record CommandResult(string? Message, bool IsError)
{
    /// <summary>Handled with no notice.</summary>
    public static CommandResult None { get; } = new((string?)null, false);

    /// <summary>Handled with an informational notice.</summary>
    public static CommandResult Notice(string message) => new(message, false);

    /// <summary>Failed with an error notice; the caller also logs the failure.</summary>
    public static CommandResult Error(string message) => new(message, true);
}
