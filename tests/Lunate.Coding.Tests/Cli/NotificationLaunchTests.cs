namespace Lunate.Coding.Tests;

/// <summary>The <c>--notify</c> launch flag: boolean, one run, and documented in <c>--help</c>.</summary>
public sealed class NotificationLaunchTests
{
    [Fact]
    public void Notify_is_off_without_the_flag_and_on_with_it()
    {
        Assert.False(Parse().Notify);
        Assert.True(Parse("--notify").Notify);
    }

    [Fact]
    public void Notify_rejects_the_inline_value_form()
    {
        Assert.False(LaunchOptions.TryParse(["--notify=true"], out _, out string? error));
        Assert.Contains("option '--notify' does not take a value", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_documents_the_flag_and_its_focus_condition()
    {
        using var writer = new StringWriter();

        var exitCode = Cli.Run(
            ["--help"],
            writer,
            TextWriter.Null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, exitCode);
        string help = writer.ToString();
        Assert.Contains("--notify", help, StringComparison.Ordinal);
        Assert.Contains("not focused", help, StringComparison.Ordinal);
    }

    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(
            LaunchOptions.TryParse(args, out LaunchOptions options, out string? error),
            error
        );
        return options;
    }
}
