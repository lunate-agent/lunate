namespace Lunate.Coding.Tests;

/// <summary>
/// The interactive session's startup line for the tool-environment pass-through: the resolved
/// settings become a visible statement about which variables a tool child can see.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class InteractiveSessionToolEnvironmentTests
{
    [Fact]
    public async Task The_startup_names_the_admitted_variables()
    {
        try
        {
            using var host = new InteractiveSessionHost();
            File.WriteAllText(
                host.Temp.File("settings.json"),
                """
                {
                  "schemaVersion": 2,
                  "toolEnvironment": { "passthrough": ["MY_PROJECT_TOKEN", "PROXY_URL"] }
                }
                """
            );

            _ = host.RunAsync();
            await host.WaitUntilAsync(() =>
            {
                host.Advance(33);
                return host.Frames.Contains(
                    "MY_PROJECT_TOKEN, PROXY_URL",
                    StringComparison.Ordinal
                );
            });

            host.Console.Complete();
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    [Fact]
    public async Task An_empty_list_stays_quiet()
    {
        try
        {
            using var host = new InteractiveSessionHost();
            _ = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            Assert.DoesNotContain(
                "toolEnvironment.passthrough",
                host.Frames,
                StringComparison.Ordinal
            );

            host.Console.Complete();
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }
}
