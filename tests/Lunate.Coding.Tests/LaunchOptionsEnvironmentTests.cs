namespace Lunate.Coding.Tests;

public sealed class LaunchOptionsEnvironmentTests
{
    [Fact]
    public void The_flag_parses_the_space_form()
    {
        Assert.True(
            LaunchOptions.TryParse(
                ["--env", "docker:alpine", "-p", "hi"],
                out LaunchOptions launch,
                out string? error
            ),
            error
        );
        Assert.Equal("docker:alpine", launch.Environment);
    }

    [Fact]
    public void The_flag_parses_the_inline_form()
    {
        Assert.True(
            LaunchOptions.TryParse(
                ["--env=podman:alpine", "-p", "hi"],
                out LaunchOptions launch,
                out string? error
            ),
            error
        );
        Assert.Equal("podman:alpine", launch.Environment);
    }

    [Fact]
    public void A_malformed_flag_is_a_usage_error()
    {
        Assert.False(
            LaunchOptions.TryParse(
                ["--env", "nerdctl:alpine", "-p", "hi"],
                out _,
                out string? error
            )
        );
        Assert.Contains(ExecutionEnvironmentSelection.Accepted, error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_flag_overrides_the_settings_value()
    {
        AgentSettings settings = new() { Environment = "podman:alpine" };
        LaunchOptions launch = new() { Environment = "docker:debian" };

        AgentSettings applied = SettingsStore.ApplyLaunch(settings, launch);

        Assert.Equal("docker:debian", applied.Environment);
    }

    [Fact]
    public void No_flag_keeps_the_settings_value()
    {
        AgentSettings settings = new() { Environment = "podman:alpine" };

        AgentSettings applied = SettingsStore.ApplyLaunch(settings, new LaunchOptions());

        Assert.Equal("podman:alpine", applied.Environment);
    }
}
