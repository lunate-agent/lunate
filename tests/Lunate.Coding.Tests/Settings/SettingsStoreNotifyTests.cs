using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The desktop-notification settings: off with a ten-second threshold by default, a non-negative
/// threshold, and the one-run <c>--notify</c> override that never rewrites the file.
/// </summary>
public sealed class SettingsStoreNotifyTests
{
    [Fact]
    public void A_missing_notify_setting_keeps_notifications_off_with_the_ten_second_threshold()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("settings.json"), """{ "schemaVersion": 2 }""");

        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

        Assert.False(settings.Notify);
        Assert.Equal(10, settings.NotifyMinSeconds);
    }

    [Fact]
    public void The_notify_setting_and_threshold_resolve()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """{ "schemaVersion": 2, "notify": true, "notifyMinSeconds": 0 }"""
        );

        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

        Assert.True(settings.Notify);
        Assert.Equal(0, settings.NotifyMinSeconds);
    }

    [Theory]
    [InlineData("""{ "notify": "on" }""", "'notify' must be true or false")]
    [InlineData(
        """{ "notifyMinSeconds": -1 }""",
        "'notifyMinSeconds' must be a non-negative integer"
    )]
    [InlineData(
        """{ "notifyMinSeconds": 1.5 }""",
        "'notifyMinSeconds' must be a non-negative integer"
    )]
    public void An_invalid_notification_setting_is_refused_with_the_value_shape(
        string notification,
        string expected
    )
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            $$"""{ "schemaVersion": 2, {{notification.Trim('{', '}')}} }"""
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
        Assert.Contains("No settings were applied", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_launch_flag_enables_notifications_without_touching_the_file()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(path, """{ "schemaVersion": 2 }""");
        AgentSettings resolved = SettingsStore.Resolve(path, _ => null);

        AgentSettings applied = SettingsStore.ApplyLaunch(
            resolved,
            new LaunchOptions { Notify = true }
        );

        Assert.True(applied.Notify);
        Assert.False(
            SettingsStore.Resolve(path, _ => null).Notify,
            "the launch flag must not rewrite the settings file"
        );
    }
}
