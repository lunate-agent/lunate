using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>toolEnvironment.passthrough</c> is user-scope only: a project settings file cannot set it, so
/// a cloned repository cannot pull a credential into a tool child. The project scope scan refuses
/// the section by name before the merge, and the tool child's environment is built from the
/// user-scope list only, with the credential strip still in force.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class ToolEnvironmentScopeTests
{
    private const string Credential = "OPENAI_API_KEY";
    private const string Secret = "fixture-only-secret";

    [Fact]
    public void A_project_scope_pass_through_is_refused_with_the_scope_rule()
    {
        using var temp = new TempDirectory();
        WriteSettings(temp, "MY_PROJECT_TOKEN");

        InvalidDataException refusal = Assert.Throws<InvalidDataException>(() => Resolve(temp));

        Assert.Contains("toolEnvironment.passthrough", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("user-scoped", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_scope_pass_through_never_reaches_the_tool_child()
    {
        try
        {
            ToolEnvironmentPassthrough.ResetForTests();
            using var temp = new TempDirectory();
            WriteSettings(temp, Credential);

            // The refusal is observable, and the project's attempt never became process-wide
            // state: the tool child is built from the user-scope list (empty here), so the
            // credential strip still removes the variable the repository named.
            Assert.Throws<InvalidDataException>(() => Resolve(temp));

            Assert.Empty(ToolEnvironmentPassthrough.Names);
            IReadOnlyDictionary<string, string> child = Lunate.Coding.ChildProcess.BuildEnvironment(
                new ProcessRunRequest(
                    "/bin/true",
                    [],
                    temp.Root,
                    Environment: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [Credential] = Secret,
                        ["PATH"] = "/usr/bin:/bin",
                    }
                )
            );

            Assert.False(child.ContainsKey(Credential));
            Assert.DoesNotContain(Secret, child.Values);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    private static void Resolve(TempDirectory temp) =>
        SettingsStore.ResolveWithProject(
            temp.File("user-settings.json"),
            temp.File("project-settings.json"),
            [],
            _ => null
        );

    private static void WriteSettings(TempDirectory temp, string name)
    {
        File.WriteAllText(temp.File("user-settings.json"), """{ "schemaVersion": 2 }""");
        File.WriteAllText(
            temp.File("project-settings.json"),
            "{ \"schemaVersion\": 2, \"toolEnvironment\": { \"passthrough\": [\"" + name + "\"] } }"
        );
    }
}
