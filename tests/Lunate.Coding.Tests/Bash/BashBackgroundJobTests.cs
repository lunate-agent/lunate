using System.Globalization;
using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The background bash family: a job starts without blocking the turn, <c>bash_output</c> reads only
/// unread output and its state, <c>bash_kill</c> ends the whole tree, the timeout still applies, and
/// the session registry kills what is left.
/// </summary>
public sealed partial class BashBackgroundJobTests
{
    private static readonly ToolContext Context = new("unused", new NullAgentEvents());

    [Fact]
    public async Task A_job_starts_and_reports_its_output_and_exit_code()
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root));
        (string id, _) = await StartAsync(tool, "echo hello; exit 7");
        var output = new BashOutputTool(tool.Jobs);

        ToolResult result = await ReadUntilAsync(output, id, text => text.Contains("exit code: 7"));

        Assert.True(result.IsError);
        Assert.Contains("hello", result.Output, StringComparison.Ordinal);
        Assert.Contains("exit code: 7", result.Output, StringComparison.Ordinal);
        Assert.Equal(new BashDetails(7), result.Details);
    }

    [Fact]
    public async Task Polling_returns_only_the_output_written_since_the_last_read()
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root));
        (string id, _) = await StartAsync(
            tool,
            "echo first; while [ ! -f go ]; do sleep 0.05; done; echo second"
        );
        var output = new BashOutputTool(tool.Jobs);
        try
        {
            ToolResult first = await ReadUntilAsync(output, id, text =>
                text.Contains("first", StringComparison.Ordinal)
            );

            Assert.Contains("first", first.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("second", first.Output, StringComparison.Ordinal);
            Assert.Contains("running", first.Output, StringComparison.Ordinal);

            File.WriteAllText(temp.File("go"), string.Empty);
            ToolResult second = await ReadUntilAsync(output, id, text =>
                text.Contains("second", StringComparison.Ordinal)
                && text.Contains("exit code: 0", StringComparison.Ordinal)
            );

            Assert.DoesNotContain("first", second.Output, StringComparison.Ordinal);
            Assert.Contains("second", second.Output, StringComparison.Ordinal);
            Assert.Contains("exit code: 0", second.Output, StringComparison.Ordinal);
        }
        finally
        {
            await tool.Jobs.KillAllAsync();
        }
    }

    [Fact]
    public async Task A_background_job_reads_end_of_file_on_stdin()
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root));
        (string id, _) = await StartAsync(tool, "cat; echo after");
        var output = new BashOutputTool(tool.Jobs);

        ToolResult result = await ReadUntilAsync(output, id, text =>
            text.Contains("exit code: 0", StringComparison.Ordinal)
        );

        Assert.False(result.IsError, result.Output);
        Assert.Contains("after", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Killing_a_job_stops_the_command_and_its_child()
    {
        using var temp = new TempDirectory();
        var pidFile = temp.File("grandchild.pid");
        var tool = new BashTool(new Workspace(temp.Root), null, WaitBudgets.CommandRun);
        DateTime commandStarted = DateTime.Now;
        using var window = new CancellationTokenSource();
        (string id, _) = await StartAsync(tool, TreeCommand());
        Task<ChildProcess> watched = ChildProcessProbe.AwaitWrittenAsync(
            pidFile,
            commandStarted,
            window.Token,
            new CommandUnderTest(temp.Root)
        );
        var grandchild = await watched;
        var kill = new BashKillTool(tool.Jobs);

        ToolResult result = await kill.ExecuteAsync(JobArgs(id), Context, CancellationToken.None);
        window.Cancel();

        Assert.False(result.IsError, result.Output);
        Assert.Contains("killed; process tree stopped", result.Output, StringComparison.Ordinal);
        Assert.False(tool.Jobs.Find(id)!.IsRunning);
        await ChildProcessProbe.AssertEventuallyDeadAsync(grandchild);
    }

    [Fact]
    public async Task The_schema_offers_background_and_rejects_a_non_boolean_value()
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root));

        Assert.True(
            tool.ParametersSchema.GetProperty("properties")
                .TryGetProperty("background", out JsonElement background)
        );
        Assert.Equal("boolean", background.GetProperty("type").GetString());
        ToolResult result = await tool.ExecuteAsync(
            Args("""{"command":"echo hello","background":"yes"}"""),
            Context,
            CancellationToken.None
        );
        Assert.True(result.IsError);
        Assert.Equal("background must be true or false", result.Output);
    }

    [Fact]
    public async Task An_unknown_job_is_a_teaching_error_for_both_tools()
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root));

        ToolResult output = await new BashOutputTool(tool.Jobs).ExecuteAsync(
            JobArgs("job-999"),
            Context,
            CancellationToken.None
        );
        ToolResult kill = await new BashKillTool(tool.Jobs).ExecuteAsync(
            JobArgs("job-999"),
            Context,
            CancellationToken.None
        );

        foreach (ToolResult result in new[] { output, kill })
        {
            Assert.True(result.IsError);
            Assert.Contains(
                "unknown background job 'job-999'",
                result.Output,
                StringComparison.Ordinal
            );
            Assert.Contains("background: true", result.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Unread_output_stays_bounded_with_a_truncation_marker()
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root), null, WaitBudgets.CommandRun);
        (string id, _) = await StartAsync(tool, "yes 0123456789 2>/dev/null | head -c 2200000");
        await WaitUntilEndedAsync(tool, id);
        var output = new BashOutputTool(tool.Jobs);

        ToolResult result = await output.ExecuteAsync(JobArgs(id), Context, CancellationToken.None);

        Assert.False(result.IsError, result.Output);
        Assert.Contains(BashJob.TruncationMarker, result.Output, StringComparison.Ordinal);
        Assert.True(
            result.Output.Length < BashTool.CaptureCap + 10_000,
            $"captured output should stay bounded, was {result.Output.Length.ToString(CultureInfo.InvariantCulture)} characters"
        );
    }

    [Fact]
    public async Task The_timeout_kills_a_background_job_tree()
    {
        using var temp = new TempDirectory();
        var pidFile = temp.File("grandchild.pid");
        DateTime commandStarted = DateTime.Now;
        var timeout = WaitBudgets.TreeKillTimeout;
        var tool = new BashTool(new Workspace(temp.Root), null, timeout);
        using var window = new CancellationTokenSource();
        (string id, _) = await StartAsync(tool, TreeCommand());
        Task<ChildProcess> watched = ChildProcessProbe.AwaitWrittenAsync(
            pidFile,
            commandStarted,
            window.Token,
            new CommandUnderTest(temp.Root)
        );
        var grandchild = await watched;
        var output = new BashOutputTool(tool.Jobs);

        ToolResult result = await ReadUntilAsync(output, id, text =>
            text.Contains("timed out after", StringComparison.Ordinal)
        );
        window.Cancel();

        Assert.True(result.IsError);
        Assert.Contains(
            $"timed out after {timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s; process tree killed",
            result.Output,
            StringComparison.Ordinal
        );
        await ChildProcessProbe.AssertEventuallyDeadAsync(grandchild);
    }

    [Fact]
    public async Task Ending_the_registry_kills_every_job_it_owns()
    {
        using var temp = new TempDirectory();
        var pidFile = temp.File("grandchild.pid");
        DateTime commandStarted = DateTime.Now;
        var tool = new BashTool(new Workspace(temp.Root), null, WaitBudgets.CommandRun);
        using var window = new CancellationTokenSource();
        (string id, _) = await StartAsync(tool, TreeCommand());
        Task<ChildProcess> watched = ChildProcessProbe.AwaitWrittenAsync(
            pidFile,
            commandStarted,
            window.Token,
            new CommandUnderTest(temp.Root)
        );
        var grandchild = await watched;

        await tool.Jobs.KillAllAsync();
        window.Cancel();

        Assert.Null(tool.Jobs.Find(id));
        await ChildProcessProbe.AssertEventuallyDeadAsync(grandchild);
        Assert.Throws<ObjectDisposedException>(() =>
            tool
                .Jobs.StartAsync("true", _ => throw new InvalidOperationException(), TimeSpan.Zero)
                .GetAwaiter()
                .GetResult()
        );
    }

    [Theory]
    [InlineData("""{"job_id":5}""", "job_id must be a string")]
    [InlineData("""{}""", "job_id is required and must be a non-empty string")]
    [InlineData("""{"job_id":""}""", "job_id is required and must be a non-empty string")]
    [InlineData("""{"job_id":"job-1","extra":true}""", "unknown argument 'extra'")]
    public async Task Malformed_output_arguments_are_errors(string arguments, string expected)
    {
        using var temp = new TempDirectory();
        var tool = new BashTool(new Workspace(temp.Root));

        ToolResult result = await new BashOutputTool(tool.Jobs).ExecuteAsync(
            Args(arguments),
            Context,
            CancellationToken.None
        );

        Assert.True(result.IsError);
        Assert.Contains(expected, result.Output, StringComparison.Ordinal);
    }
}
