namespace Lunate.Coding.Tests;

/// <summary>
/// The startup wiring of <c>toolEnvironment.passthrough</c>: a print run installs the configured
/// list and surfaces one warning when a listed name is also a Lunate credential name, on stderr,
/// without turning the run into an error.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class ToolEnvironmentPrintModeTests
{
    [Fact]
    public async Task A_listed_credential_name_warns_once_and_the_run_succeeds()
    {
        try
        {
            using var temp = new TempDirectory();
            File.WriteAllText(
                temp.File("settings.json"),
                """
                {
                  "schemaVersion": 2,
                  "toolEnvironment": { "passthrough": ["OPENAI_API_KEY"] }
                }
                """
            );
            var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());
            PrintModeOptions options = PrintModeTestSupport.BaseOptions(
                temp,
                new FakeChatClientFactory(client)
            ) with
            {
                Prompt = "Hi",
            };

            (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(
                options
            );

            Assert.Equal(0, exitCode);
            Assert.Equal("ok", output);
            string[] lines = errors.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string warning = Assert.Single(
                lines,
                line => line.Contains("toolEnvironment.passthrough", StringComparison.Ordinal)
            );
            Assert.Contains("OPENAI_API_KEY", warning, StringComparison.Ordinal);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    [Fact]
    public async Task A_user_owned_name_installs_without_a_warning()
    {
        try
        {
            using var temp = new TempDirectory();
            File.WriteAllText(
                temp.File("settings.json"),
                """
                {
                  "schemaVersion": 2,
                  "toolEnvironment": { "passthrough": ["MY_PROJECT_TOKEN"] }
                }
                """
            );
            var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());
            PrintModeOptions options = PrintModeTestSupport.BaseOptions(
                temp,
                new FakeChatClientFactory(client)
            ) with
            {
                Prompt = "Hi",
            };

            (int exitCode, _, string errors) = await PrintModeTestSupport.RunAsync(options);

            Assert.Equal(0, exitCode);
            Assert.DoesNotContain("toolEnvironment.passthrough", errors, StringComparison.Ordinal);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }
}
