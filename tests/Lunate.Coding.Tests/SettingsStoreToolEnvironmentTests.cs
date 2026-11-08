using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The tool-process pass-through setting (<c>toolEnvironment.passthrough</c>): the exact names a
/// user admits into tool children after the inherited policy's credential strip. A missing key is
/// the empty list; every bad shape is collected into the one settings error.
/// </summary>
public sealed class SettingsStoreToolEnvironmentTests
{
    [Fact]
    public void A_missing_key_defaults_the_pass_through_to_empty()
    {
        using var temp = new TempDirectory();

        AgentSettings settings = SettingsStore.Resolve(temp.File("missing.json"), _ => null);

        Assert.Empty(settings.ToolEnvironment.Passthrough);
    }

    [Fact]
    public void A_settings_file_reads_the_exact_names_in_order()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "toolEnvironment": { "passthrough": ["MY_PROJECT_TOKEN", "PROXY_URL"] }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

        Assert.Equal(["MY_PROJECT_TOKEN", "PROXY_URL"], settings.ToolEnvironment.Passthrough);
    }

    [Fact]
    public void A_value_with_an_equals_sign_is_refused_as_a_name()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "toolEnvironment": { "passthrough": ["MY_PROJECT_TOKEN=secret"] }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains("toolEnvironment.passthrough", exception.Message, StringComparison.Ordinal);
        Assert.Contains("environment variable names", exception.Message, StringComparison.Ordinal);
        Assert.Contains("No settings were applied", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_name_and_a_duplicate_are_both_refused()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "toolEnvironment": { "passthrough": ["MY_PROJECT_TOKEN", "  ", "MY_PROJECT_TOKEN"] }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains("toolEnvironment.passthrough", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MY_PROJECT_TOKEN", exception.Message, StringComparison.Ordinal);
        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_section_and_its_list_must_be_the_declared_shapes()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "toolEnvironment": { "passthrough": "MY_PROJECT_TOKEN" }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            "'toolEnvironment.passthrough' must be an array",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void An_unknown_key_inside_the_section_is_reported()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "toolEnvironment": { "inherit": true }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            "unknown key 'toolEnvironment.inherit'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_section_must_be_an_object()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "toolEnvironment": ["MY_PROJECT_TOKEN"]
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            "'toolEnvironment' must be an object",
            exception.Message,
            StringComparison.Ordinal
        );
    }
}
