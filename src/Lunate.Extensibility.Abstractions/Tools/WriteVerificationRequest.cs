using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

/// <summary>A read-only request to verify the exact normalized text a host is about to write.</summary>
public sealed class WriteVerificationRequest(
    string relativePath,
    string baselineContent,
    string stagedContent,
    WorkspaceInfo workspace
)
{
    public string RelativePath { get; } = relativePath;

    public string BaselineContent { get; } = baselineContent;

    public string StagedContent { get; } = stagedContent;

    public WorkspaceInfo Workspace { get; } = workspace;
}
