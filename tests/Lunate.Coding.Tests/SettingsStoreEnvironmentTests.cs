using System.Reflection;

namespace Lunate.Coding.Tests;

public sealed class SettingsStoreEnvironmentTests
{
    [Fact]
    public void No_environment_setting_keeps_the_host_path()
    {
        using var temp = new TempDirectory();

        AgentSettings settings = SettingsStore.Resolve(temp.File("missing.json"), _ => null);

        Assert.Null(ReadEnvironment(settings));
    }

    [Fact]
    public void The_settings_key_selects_docker_or_podman()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """{"schemaVersion":2,"environment":"podman:alpine"}"""
        );

        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

        Assert.Equal("podman:alpine", ReadEnvironment(settings));
    }

    [Fact]
    public void A_malformed_environment_is_refused_whole()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """{"schemaVersion":2,"environment":"docker:"}"""
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            ExecutionEnvironmentSelection.Accepted,
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_environment_setting_is_not_a_public_dotnet_member()
    {
        PropertyInfo? property = typeof(AgentSettings).GetProperty(
            "Environment",
            BindingFlags.Instance | BindingFlags.Public
        );

        Assert.Null(property);
    }

    private static string? ReadEnvironment(AgentSettings settings) =>
        (string?)
            typeof(AgentSettings)
                .GetProperty("Environment", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(settings);
}
