using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The provider-native search opt-in through print mode: the setting reaches the harness, and the
/// request's model must declare the <c>web_search</c> feature for the built-in search to replace the
/// local declaration. The stub provider observes the request options; no network is involved.
/// </summary>
public sealed class NativeWebSearchPrintModeTests
{
    [Fact]
    public async Task The_opt_in_and_a_declaring_model_declare_the_native_search()
    {
        using var temp = new TempDirectory();
        WriteModel(temp, features: ["tools", "web_search"]);
        WriteSettings(temp, nativeSearch: true);
        var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());

        (int exitCode, string output, string errors) = await RunAsync(temp, client);

        Assert.Equal(0, exitCode);
        Assert.Equal("ok", output);
        Assert.Empty(errors);
        IList<AITool> tools = client.RequestOptions[0]!.Tools!;
        Assert.Single(tools.OfType<HostedWebSearchTool>());
        Assert.DoesNotContain(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_search", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Without_the_opt_in_the_local_search_is_declared()
    {
        using var temp = new TempDirectory();
        WriteModel(temp, features: ["tools", "web_search"]);
        var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());

        (int exitCode, string output, string errors) = await RunAsync(temp, client);

        Assert.Equal(0, exitCode);
        Assert.Equal("ok", output);
        Assert.Empty(errors);
        IList<AITool> tools = client.RequestOptions[0]!.Tools!;
        Assert.DoesNotContain(tools, tool => tool is HostedWebSearchTool);
        Assert.Contains(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_search", StringComparison.Ordinal)
        );
    }

    private static async Task<(int ExitCode, string Output, string Errors)> RunAsync(
        TempDirectory temp,
        ScriptedChatClient client
    )
    {
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Hi",
        };
        return await PrintModeTestSupport.RunAsync(options);
    }

    private static void WriteModel(TempDirectory temp, string[] features) =>
        File.WriteAllText(
            temp.File("models.json"),
            $$"""
            {
              "schemaVersion": 2,
              "models": [
                {
                  "id": "{{PrintModeTestSupport.ModelId}}",
                  "provider": "openai",
                  "contextWindow": 128000,
                  "supportsTools": true,
                  "capabilities": { "features": [{{string.Join(
                ", ",
                features.Select(feature => $"\"{feature}\"")
            )}}] }
                }
              ]
            }
            """
        );

    private static void WriteSettings(TempDirectory temp, bool nativeSearch) =>
        File.WriteAllText(
            temp.File("settings.json"),
            $$"""
            {
              "schemaVersion": 2,
              "tools": { "nativeSearch": {{(nativeSearch ? "true" : "false")}} }
            }
            """
        );
}
