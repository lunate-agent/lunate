namespace Lunate.Coding.Tests;

/// <summary>
/// The tool-process environment is process-wide state (like <see cref="CredentialVariables"/>), so
/// the tests that configure it and the tests that spawn a child which reads it share one xUnit
/// collection and run sequentially.
/// </summary>
internal static class ToolEnvironmentCollection
{
    internal const string Name = "tool-process-environment";
}
