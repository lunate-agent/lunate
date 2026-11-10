using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The real-container proof, opt-in with <c>LUNATE_CONTAINER_E2E=&lt;engine&gt;:&lt;image&gt;</c> so
/// the local gate does not require an engine. Linux CI runs it with Docker; the spike's macOS
/// Podman run is the other measured host.
/// </summary>
public sealed class ContainerEnvironmentE2ETests
{
    private const string EnvironmentVariable = "LUNATE_CONTAINER_E2E";

    private static readonly ToolContext Context = new("run", new NullAgentEvents());

    [Fact]
    public async Task The_container_shares_the_workspace_and_cancel_ends_the_command()
    {
        string? value = System.Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipUnless(
            ExecutionEnvironmentSelection.TryParse(
                value,
                out ExecutionEnvironmentSelection? parsed,
                out _
            ) && parsed is not null,
            $"{EnvironmentVariable} must name <engine>:<image>."
        );
        ExecutionEnvironmentSelection selection = parsed!;

        using var temp = new TempDirectory();
        using var environment = new ContainerEnvironment(selection);
        var tool = new BashTool(
            new Workspace(temp.Root),
            null,
            WaitBudgets.ContainerE2E,
            null,
            new BashGuard(),
            changeScanTimeout: null,
            guardEnabled: true,
            environment: environment
        );

        ToolResult echo = await Run(tool, "echo hello");
        Assert.False(echo.IsError, echo.Output);
        Assert.Contains("hello", echo.Output, StringComparison.Ordinal);

        ToolResult write = await Run(tool, "printf 'container' > probe.txt");
        Assert.False(write.IsError, write.Output);
        Assert.Equal("container", File.ReadAllText(Path.Combine(temp.Root, "probe.txt")));

        var read = new ReadTool(new Workspace(temp.Root));
        ToolResult hostRead = await read.ExecuteAsync(
            JsonDocument.Parse("""{"path":"probe.txt"}""").RootElement,
            Context,
            CancellationToken.None
        );
        Assert.False(hostRead.IsError, hostRead.Output);
        Assert.Contains("container", hostRead.Output, StringComparison.Ordinal);

        ToolResult marker = await Run(tool, "printf '%s' \"$LUNATE_SESSION\"");
        Assert.Equal("1", marker.Output.Replace("\nexit code: 0", string.Empty));

        ToolResult absent = await Run(
            tool,
            $"test -e \"{LunateHome.Store}\" && echo present || echo absent"
        );
        Assert.Contains("absent", absent.Output, StringComparison.Ordinal);

        ToolResult cancelled = await Run(tool, "sleep 4271");
        Assert.True(cancelled.IsError);
        Assert.Contains("timed out", cancelled.Output, StringComparison.Ordinal);
        ToolResult survivors = await Run(
            tool,
            """sh -c 'pgrep -f "slee[p] 4271" >/dev/null && echo alive || echo gone'"""
        );
        Assert.Contains("gone", survivors.Output, StringComparison.Ordinal);

        ToolResult after = await Run(tool, "echo after");
        Assert.False(after.IsError, after.Output);
        Assert.Contains("after", after.Output, StringComparison.Ordinal);
    }

    private static Task<ToolResult> Run(BashTool tool, string command) =>
        tool.ExecuteAsync(
            JsonDocument.Parse(JsonSerializer.Serialize(new { command })).RootElement,
            Context,
            CancellationToken.None
        );
}
