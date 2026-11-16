using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// AGENTS.md: the bytes of a file Lunate owns are LF and UTF-8 without BOM on every platform. The
/// indented JSON writer defaults to <see cref="Environment.NewLine"/>, so both store option sets
/// pin <c>NewLine = "\n"</c>; the written bytes are the contract, and a Windows runner is the leg
/// that shows a regression.
/// </summary>
public sealed class StoreLineEndingTests
{
    [Fact]
    public void Settings_store_writes_carry_no_carriage_return()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");

        SettingsStore.WriteProfile(new ModelProfile("chat-main", "openai", "gpt-5"), path);
        SettingsStore.WriteUsage("chat", "chat-main", path);
        SettingsStore.WriteDecisionsKey("tool", "on", path);
        SettingsStore.WriteFavorites(["gpt-5"], path);

        AssertLfOnly(path);

        // The body is the same document on every platform: the parser accepts it and the bytes
        // carry one newline per line, never a platform pair.
        AgentSettings settings = SettingsStore.Resolve(path, _ => null);
        Assert.Equal(["gpt-5"], settings.Favorites);
        Assert.Contains("chat-main", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_store_writes_carry_no_carriage_return()
    {
        using var temp = new TempDirectory();
        string path = temp.File("providers.json");

        ProviderStore.Save(
            path,
            [
                new ProviderEntry(
                    "openai",
                    "openai",
                    new Uri("https://api.openai.com/v1"),
                    "auth:openai"
                ),
            ]
        );

        AssertLfOnly(path);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(
            "openai",
            document.RootElement.GetProperty("providers")[0].GetProperty("name").GetString()
        );
    }

    [Fact]
    public void Auth_store_writes_carry_no_carriage_return()
    {
        using var temp = new TempDirectory();
        string path = temp.File("auth.json");
        AuthStore auth = AuthStore.Load(path, _ => null);
        auth.Set("openai", "fixture-only-secret");
        auth.Save();

        AssertLfOnly(path);
    }

    private static void AssertLfOnly(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Contains((byte)'\n', bytes);
        Assert.NotEqual((byte)0xEF, bytes[0]);
    }
}
