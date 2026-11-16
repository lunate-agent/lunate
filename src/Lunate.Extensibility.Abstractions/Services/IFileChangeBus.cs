namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// Read-only handle to the core file change bus. A subscription may carry a pattern: <c>*</c>
/// matches any characters and <c>?</c> exactly one; patterns match the whole canonical absolute
/// path and compare case-sensitively on Linux, case-insensitively elsewhere. A null or empty
/// pattern matches every change.
/// </summary>
public interface IFileChangeBus
{
    /// <summary>Subscribes for the extension's lifetime; disposal ends the subscription.</summary>
    IDisposable Subscribe(IFileChangedHandler handler, string? pathPattern = null);
}
