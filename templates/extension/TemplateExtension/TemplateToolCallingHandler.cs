using Lunate.Extensibility.Abstractions;

namespace TemplateExtension;

/// <summary>
/// Logs every tool call before approval and lets it proceed with the arguments unchanged.
/// Return <see cref="ToolCallingResult.Block"/> to refuse a call, or
/// <see cref="ToolCallingResult.Proceed"/> with new arguments to mutate them.
/// </summary>
public sealed class TemplateToolCallingHandler(string extensionId, IExtensionLog log)
    : IToolCallingHandler
{
    public int Priority => 0;

    public ValueTask<ToolCallingResult> HandleAsync(
        ToolCallingPayload payload,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(payload);
        log.Info(
            $"template[{extensionId}]: tool-calling hook saw '{payload.ToolName}' (call {payload.CallId})"
        );
        ToolCallingResult result = new ToolCallingResult.Proceed(null);
        log.Info($"template[{extensionId}]: tool-calling decision {Describe(result)}");
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Names every case this handler knows. The <c>_</c> arm is the one the template's README asks
    /// for: a copy pins the published contract, which is older than the host's case set (#933).
    /// </summary>
    private static string Describe(ToolCallingResult result) =>
        result switch
        {
            ToolCallingResult.Proceed => "proceed",
            ToolCallingResult.Block blocked => $"block ({blocked.Reason})",
            _ => "unrecognised",
        };
}
