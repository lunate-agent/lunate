using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// A profile switch re-seeds the session thinking level: the newly selected profile's
/// <c>reasoningLevel</c> (or the settings default when it has none) replaces the previous
/// profile's level before the harness is rebuilt, so the next request carries the new level and
/// the footer and <c>/thinking</c> agree.
/// </summary>
public sealed class ThinkingProfileSwitchTests
{
    [Fact]
    public async Task Switching_profiles_reseeds_the_level_for_the_next_request()
    {
        using var host = new InteractiveSessionHost();
        File.WriteAllText(
            host.Temp.File("models.json"),
            $$"""
            {
              "schemaVersion": 1,
              "models": [
                {
                  "id": "{{PrintModeTestSupport.ModelId}}",
                  "provider": "openai",
                  "endpoint": null,
                  "contextWindow": 128000,
                  "supportsTools": true,
                  "reasoning": "effort"
                }
              ]
            }
            """
        );
        File.WriteAllText(
            host.Temp.File("settings.json"),
            $$"""
            {
              "schemaVersion": 2,
              "profiles": {
                "profile-a": {
                  "provider": "chat-provider",
                  "model": "{{PrintModeTestSupport.ModelId}}",
                  "reasoningLevel": "high"
                },
                "profile-b": {
                  "provider": "chat-provider",
                  "model": "{{PrintModeTestSupport.ModelId}}",
                  "reasoningLevel": "low"
                }
              },
              "usages": { "chat": "profile-a" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [ProfileProvider()]
        );
        Task run = host.RunAsync();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:high");

        host.Client.Enqueue(Scripts.Text("first"), Scripts.Stop());
        host.Console.SendText("hi");
        host.Console.SendEnter();
        await host.Client.WaitForCallAsync(1);
        Assert.Equal(ReasoningEffort.High, host.Client.RequestOptions[0]!.Reasoning?.Effort);
        await host.WaitUntilAsync(() => !host.Session.IsRunning);

        host.Console.SendText("/model profile-b");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:low");

        host.Console.SendText("/thinking");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "thinking: low");
        Assert.Contains("reasoning effort low", host.LastFrame, StringComparison.Ordinal);

        host.Client.Enqueue(Scripts.Text("second"), Scripts.Stop());
        host.Console.SendText("again");
        host.Console.SendEnter();
        await host.Client.WaitForCallAsync(2);
        Assert.Equal(ReasoningEffort.Low, host.Client.RequestOptions[1]!.Reasoning?.Effort);

        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    private static ProviderEntry ProfileProvider()
    {
        bool valid = ProviderEntry.TryCreate(
            "chat-provider",
            "openai",
            new Uri("https://provider.example.test/v1"),
            "auth:chat-provider",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );
        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }
}
