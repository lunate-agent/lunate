namespace Lunate.Coding.Tests;

/// <summary>
/// The tool-process environment is process-wide state (like <see cref="CredentialVariables"/>), and
/// every session composition configures it from its resolved settings. A test that reads or
/// configures the list therefore must not run beside any other test: a parallel session's
/// <c>Configure(empty)</c> would clear it between a test's configure and its spawn. The collection
/// runs its own tests sequentially, and <c>DisableParallelization</c> makes it exclusive with every
/// other collection too — the same shape as the other process-global collections.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ToolEnvironmentCollection
{
    public const string Name = "tool-process-environment";
}
