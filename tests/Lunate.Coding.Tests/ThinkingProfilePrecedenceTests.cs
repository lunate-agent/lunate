using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The thinking precedence: the chat profile's <c>reasoningLevel</c> is the session's default
/// (under the launch flag, over the global <c>thinking</c> key), <c>/thinking</c> changes the
/// session only and never the profile, <c>/settings thinking</c> persists the profile persistently,
/// and a model without reasoning support refuses the setting by name.
/// </summary>
public sealed class ThinkingProfilePrecedenceTests
{
    [Fact]
    public async Task The_chat_profiles_reasoning_level_is_the_session_default()
    {
        (InteractiveSessionHost host, Task run) = await ReasoningProfileHostAsync(
            profileLevel: "high"
        );

        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:high");
        Assert.Contains("think:high", host.LastFrame, StringComparison.Ordinal);

        host.Client.Enqueue(Scripts.Text("ok"), Scripts.Stop());
        host.Console.SendText("hi");
        host.Console.SendEnter();
        await host.Client.WaitForCallAsync(1);
        Assert.Equal(ReasoningEffort.High, host.Client.RequestOptions[0]!.Reasoning?.Effort);

        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task The_launch_flag_wins_over_the_profile()
    {
        (InteractiveSessionHost host, Task run) = await ReasoningProfileHostAsync(
            profileLevel: "high",
            launch: new LaunchOptions { Thinking = ThinkingLevel.Off }
        );

        await host.WaitUntilAsync(() => host.Session.IsComposed);
        Assert.DoesNotContain("think:", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task The_profile_wins_over_the_global_thinking_key()
    {
        (InteractiveSessionHost host, Task run) = await ReasoningProfileHostAsync(
            profileLevel: "high",
            fileThinking: "low"
        );

        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:high");
        Assert.Contains("think:high", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task The_global_thinking_key_fills_a_silent_profile()
    {
        (InteractiveSessionHost host, Task run) = await ReasoningProfileHostAsync(
            fileThinking: "low"
        );

        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:low");
        Assert.Contains("think:low", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task An_unsupported_model_refuses_the_thinking_setting_by_name()
    {
        using var host = new InteractiveSessionHost();
        Task run = host.RunAsync();

        host.Console.SendText("/thinking high");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "has no reasoning support");

        Assert.Contains(
            "model '" + PrintModeTestSupport.ModelId + "' has no reasoning support "
                + "— the 'thinking' setting is refused",
            host.LastFrame,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("think:", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/thinking");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "thinking: off");
        Assert.Contains("thinking: off", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task Thinking_leaves_the_profile_and_the_settings_file_as_they_were()
    {
        (InteractiveSessionHost host, Task run) = await ReasoningProfileHostAsync(
            profileLevel: "high"
        );

        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:high");
        host.Console.SendText("/thinking off");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "thinking: off");
        Assert.DoesNotContain("think:", host.LastFrame, StringComparison.Ordinal);

        string file = File.ReadAllText(host.Temp.File("settings.json"));
        Assert.Contains("\"high\"", file, StringComparison.Ordinal);
        Assert.DoesNotContain("\"thinking\"", file, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task Settings_thinking_persists_the_chat_profiles_level()
    {
        (InteractiveSessionHost host, Task run) = await ReasoningProfileHostAsync(
            profileLevel: "high"
        );

        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "think:high");
        host.Console.SendText("/settings thinking low");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "thinking: low");
        Assert.Contains(
            "thinking: low (saved to " + host.Temp.File("settings.json") + ")",
            host.LastFrame,
            StringComparison.Ordinal
        );
        Assert.Contains("think:low", host.LastFrame, StringComparison.Ordinal);

        string file = File.ReadAllText(host.Temp.File("settings.json"));
        Assert.Contains("\"reasoningLevel\": \"low\"", file, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reasoningLevel\": \"high\"", file, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task Settings_thinking_refuses_a_selection_derived_profile()
    {
        using var host = new InteractiveSessionHost();
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/settings thinking low");
        host.Console.SendEnter();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "cannot be persisted");

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    /// <summary>
    /// A host over a settings file whose chat profile is <c>chat-profile</c> on the chat provider
    /// and the default catalog model: <paramref name="profileLevel"/> seeds the profile's
    /// <c>reasoningLevel</c> (and makes the catalog model an <c>effort</c> reasoning model), while
    /// <paramref name="fileThinking"/> sets the file's global <c>thinking</c> key.
    /// </summary>
    private static async Task<(InteractiveSessionHost Host, Task Run)> ReasoningProfileHostAsync(
        string? profileLevel = null,
        string? fileThinking = null,
        LaunchOptions? launch = null
    )
    {
        var host = new InteractiveSessionHost(launch: launch);
        bool reasoned = profileLevel is not null;
        File.WriteAllText(
            host.Temp.File("models.json"),
            $"""
            {{
              "schemaVersion": 1,
              "models": [
                {{
                  "id": "{PrintModeTestSupport.ModelId}",
                  "provider": "openai",
                  "endpoint": null,
                  "contextWindow": 128000,
                  "supportsTools": true{(
                      reasoned ? $", \"reasoning\": \"effort\"" : string.Empty
                  )}
                }}
              ]
            }}
            """
        );
        File.WriteAllText(
            host.Temp.File("settings.json"),
            $"""
            {{
              "schemaVersion": 2,
              "profiles": {{
                "chat-profile": {{
                  "provider": "chat-provider",
                  "model": "{PrintModeTestSupport.ModelId}"{(
                      reasoned ? $", \"reasoningLevel\": \"{profileLevel}\"" : string.Empty
                  )}
                }}
              }},
              "usages": {{ "chat": "chat-profile" }}{(
                  fileThinking is null
                      ? string.Empty
                      : $",\n  \"thinking\": \"{fileThinking}\""
              )}
            }}
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [ProfileProvider()]
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);
        return (host, run);
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
