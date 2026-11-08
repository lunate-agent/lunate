namespace Lunate.Coding.Tests;

/// <summary>
/// The one accepted environment shape: <c>docker:&lt;image&gt;</c> or <c>podman:&lt;image&gt;</c>.
/// A malformed value is refused, never treated as host execution.
/// </summary>
public sealed class ExecutionEnvironmentSelectionTests
{
    [Theory]
    [InlineData("docker:alpine", "docker", "alpine")]
    [InlineData("podman:ghcr.io/example/tools:1.2", "podman", "ghcr.io/example/tools:1.2")]
    [InlineData("  docker:debian  ", "docker", "debian")]
    public void Accepted_shapes_parse(string value, string engine, string image)
    {
        Assert.True(
            ExecutionEnvironmentSelection.TryParse(
                value,
                out ExecutionEnvironmentSelection? selection,
                out string? error
            ),
            error
        );

        Assert.NotNull(selection);
        Assert.Equal(engine, selection.Engine);
        Assert.Equal(image, selection.Image);
        Assert.Equal($"{engine}:{image}", selection.Display);
    }

    [Theory]
    [InlineData("nerdctl:alpine")]
    [InlineData("docker:")]
    [InlineData("docker")]
    [InlineData(":alpine")]
    [InlineData("docker:two words")]
    [InlineData("Docker:alpine")]
    public void Malformed_shapes_are_refused(string value)
    {
        Assert.False(
            ExecutionEnvironmentSelection.TryParse(
                value,
                out ExecutionEnvironmentSelection? selection,
                out string? error
            )
        );
        Assert.Null(selection);
        Assert.Contains(ExecutionEnvironmentSelection.Accepted, error, StringComparison.Ordinal);
        Assert.Contains(value, error, StringComparison.Ordinal);
    }

    [Fact]
    public void No_value_means_host_execution_without_an_error()
    {
        Assert.True(
            ExecutionEnvironmentSelection.TryParse(
                null,
                out ExecutionEnvironmentSelection? selection,
                out string? error
            )
        );
        Assert.Null(selection);
        Assert.Null(error);
    }
}
