using Lunate.Extensibility;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// ADR-0040's conformance proof: an extension observes one turn from start to end through
/// <see cref="Lunate.Extensibility.Abstractions.IExtensionContext.Events"/> and registers no hook
/// handler, so nothing about the run can change — and the run's answer is exactly what the model
/// scripted.
/// </summary>
public sealed class ExtensionEventStreamConformanceTests
{
    private const string ExtensionId = "observer";

    [Fact]
    public void An_observer_sees_a_print_turn_from_start_to_end_without_intercepting_it()
    {
        using var temp = new TempDirectory();
        string store = temp.File("store");
        string marker = temp.File("observed.txt");
        Install(store, marker);

        var log = new RecordingExtensionLog();
        var client = new ScriptedChatClient().Enqueue(Scripts.Text("the answer"), Scripts.Stop());
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        );
        using var output = new StringWriter();
        using var errors = new StringWriter();

        int exitCode = Cli.Run(
            ["-p", "say hi"],
            output,
            errors,
            TestContext.Current.CancellationToken,
            options,
            extensions: new ExtensionSeams { StorePath = store, Log = log }
        );

        Assert.Equal(0, exitCode);
        // The observer changed nothing: the answer is the scripted response, not a hooked one.
        Assert.Equal("the answer", output.ToString());

        IReadOnlyList<string> observed = File.ReadAllLines(marker);
        Assert.Equal("RunStarted", observed[0]);
        Assert.Equal("completed", observed[^1]);
        Assert.Equal("RunFinished", observed[^2]);
        Assert.Contains("TextMessageEnd", observed);
        Assert.DoesNotContain(log.Messages, message =>
            message.Contains("threw from its event subscription", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task The_observer_registers_no_hook_handler_and_its_subscription_is_live()
    {
        using var temp = new TempDirectory();
        string store = temp.File("store");
        string marker = temp.File("observed.txt");
        Install(store, marker);

        await using ExtensionSession session = await ExtensionSession.StartAsync(
            new WorkspaceInfo(temp.File("worktree"), null, null),
            new ExtensionSeams { StorePath = store, Log = new RecordingExtensionLog() },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, session.HookRunner.Count(ExtensionId));
        ExtensionEventStreamStatus status = session.EventStatus(ExtensionId);
        Assert.True(status.Subscribed);
        Assert.Equal(0, status.Delivered);
        Assert.DoesNotContain(session.Descriptors, descriptor =>
            descriptor.Manifest.Hooks.Count > 0
        );
    }

    /// <summary>
    /// Installs the built observer fixture under the store as a hand-placed global extension (trusted
    /// by placement, so no prompt) with a watch list and a marker path in its settings.
    /// </summary>
    private static void Install(string store, string marker)
    {
        string directory = Path.Combine(store, "extensions", ExtensionId);
        Directory.CreateDirectory(directory);
        HelloFixture.CopyDirectory(
            Path.Combine(AppContext.BaseDirectory, "TestExtensions", "ObserverExtension"),
            directory
        );
        File.WriteAllText(
            Path.Combine(directory, "extension.json"),
            $$"""{"id":"{{ExtensionId}}","version":"0.1.0","apiVersion":"^1.0.0","entryAssembly":"ObserverExtension.dll","events":["*"]}"""
        );
        HelloFixture.WriteSettings(store, ExtensionId, marker);
    }
}
