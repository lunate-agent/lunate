namespace Lunate.Extensibility.Abstractions;

/// <summary>Core workspace identity exposed to extensions.</summary>
public sealed record WorkspaceInfo(string WorktreeRoot, string? RepoRoot, string? GitCommonDir);
