using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

/// <summary>A verification outcome, with optional user/model-facing reason and structured diagnostics.</summary>
public sealed class WriteVerificationResult(
    WriteVerificationStatus status,
    string? reason = null,
    JsonElement? structuredContent = null
)
{
    public WriteVerificationStatus Status { get; } = status;

    public string? Reason { get; } = reason;

    public JsonElement? StructuredContent { get; } = structuredContent;
}
