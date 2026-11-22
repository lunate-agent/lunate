using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;
using Lunate.Extensibility.Remote;
using Lunate.Extensibility.Testing;

namespace Lunate.Extensibility.Remote.Tests;

/// <summary>
/// Remote extensions through the real loader and hook runner: discovery and trust, lazy load
/// with declaration-driven proxies, background startup after the UI is ready, failed extensions
/// contained, the trust digest covering the bundled files, and the manual restart path.
/// </summary>
public sealed class LoaderRemoteTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_remote_extension_loads_lazily_and_answers_through_the_hook_runner()
    {
        using var workspace = new RemoteTestWorkspace();
        ScriptedChild child = workspace.Child;
        child.HookAnswers = (_, _, _) =>
            ValueTask.FromResult<JsonElement?>(
                HookResultCodec.EncodeResult(
                    HookContracts.Find("ToolCalling")!,
                    new ToolCallingResult.Block("held")
                )
            );
        ExtensionLoader loader = workspace.CreateLoader();

        LoadedExtension loaded = await workspace.LoadAsync(loader);

        Assert.Equal("remote-sample", loaded.Id);
        Assert.Equal(0, child.StartCount);
        Assert.Equal(1, loader.Hooks.Count("remote-sample"));

        ToolCallingResult result = await loader.Hooks.RunToolCallingAsync(
            new ToolCallingPayload(
                "r1",
                "c1",
                "read",
                JsonDocument.Parse("{}").RootElement.Clone()
            ),
            Ct
        );

        Assert.Equal(new ToolCallingResult.Block("held"), result);
        Assert.Equal(1, child.StartCount);
    }

    [Fact]
    public async Task A_session_started_subscriber_starts_only_when_the_session_is_live()
    {
        using var workspace = new RemoteTestWorkspace(hooks: ["session-started"]);
        ScriptedChild child = workspace.Child;
        ExtensionLoader loader = workspace.CreateLoader();

        // The session-start dispatch fires after the load: the child must not start (never on the
        // startup path) and the payload is held.
        LoadedExtension loaded = await workspace.LoadAsync(loader);

        Assert.Equal("remote-sample", loaded.Id);
        Assert.Equal(0, child.StartCount);

        await loader.StartSessionAsync(workspace.Worktree, "repo", Ct);

        Assert.Equal(0, child.StartCount);

        // The post-ready point delivers the deferred session-started and starts the child then.
        await loader.StartBackgroundExtensionsAsync(Ct);

        await WaitAsync(() => child.StartCount == 1, "the child started post-ready");
        await WaitAsync(
            () => child.Notifications.Any(n => n.Method == "hook/SessionStarted"),
            "the deferred session-started arrived"
        );
    }

    [Fact]
    public async Task A_deferred_session_started_is_delivered_on_the_first_lazy_use()
    {
        using var workspace = new RemoteTestWorkspace(hooks: ["session-started", "tool-calling"]);
        ScriptedChild child = workspace.Child;
        ExtensionLoader loader = workspace.CreateLoader();

        LoadedExtension loaded = await workspace.LoadAsync(loader);

        Assert.Equal(0, child.StartCount);

        await loader.StartSessionAsync(workspace.Worktree, "repo", Ct);

        // No ready point in this flow: the first use starts the child and the held
        // session-started is delivered before the triggering hook.
        await loader.Hooks.RunToolCallingAsync(
            new ToolCallingPayload(
                "r1",
                "c1",
                "read",
                JsonDocument.Parse("{}").RootElement.Clone()
            ),
            Ct
        );

        await WaitAsync(() => child.StartCount == 1, "the child started on first use");
        // The notification is written before the triggering hook proceeds, but the child records
        // it on its own read loop — wait for it instead of racing that loop.
        await WaitAsync(
            () => child.Notifications.Any(n => n.Method == "hook/SessionStarted"),
            "the deferred session-started arrived"
        );
    }

    [Fact]
    public async Task An_events_only_extension_starts_at_session_start_and_observes()
    {
        // No hook, service or command: only the declared watch list would ever bring this child to
        // life, and it must be alive before the first run emits (ADR-0040).
        using var workspace = new RemoteTestWorkspace(hooks: [], events: ["*"]);
        ExtensionLoader loader = workspace.CreateLoader();

        await workspace.LoadAsync(loader);

        Assert.Equal(1, workspace.Child.StartCount);
        loader.EventStream.Publish(new Lunate.Agent.RunStarted("run_1"));
        await WaitAsync(
            () => workspace.Child.ObservedEventKinds().Contains("run_started"),
            "the projected event"
        );
    }

    [Fact]
    public async Task An_events_declaration_starts_a_background_child_at_session_start()
    {
        // `background` moves the deferred session-started to the post-ready point, but an observer
        // that waited for it would miss the first run: the events declaration starts the child at
        // session start even in background mode (the one startup-path exception).
        using var workspace = new RemoteTestWorkspace(events: ["*"], startup: "background");
        ExtensionLoader loader = workspace.CreateLoader();

        await workspace.LoadAsync(loader);

        Assert.Equal(1, workspace.Child.StartCount);
        loader.EventStream.Publish(new RunStarted("run_1"));
        await WaitAsync(
            () => workspace.Child.ObservedEventKinds().Contains("run_started"),
            "the projected event"
        );
    }

    [Fact]
    public async Task Background_mode_starts_after_the_explicit_trigger_only()
    {
        using var workspace = new RemoteTestWorkspace(startup: "background");
        ExtensionLoader loader = workspace.CreateLoader();

        await workspace.LoadAsync(loader);

        Assert.Equal(0, workspace.Child.StartCount);

        await loader.StartBackgroundExtensionsAsync(Ct);

        Assert.Equal(1, workspace.Child.StartCount);
    }

    [Fact]
    public async Task A_refused_remote_extension_is_contained_and_the_session_continues()
    {
        using var workspace = new RemoteTestWorkspace();
        workspace.Child.Handshake = request => new InitializeResult(99, request.Hooks, []);
        ExtensionLoader loader = workspace.CreateLoader();
        await workspace.LoadAsync(loader);

        ToolCallingResult result = await loader.Hooks.RunToolCallingAsync(
            new ToolCallingPayload(
                "r1",
                "c1",
                "read",
                JsonDocument.Parse("{}").RootElement.Clone()
            ),
            Ct
        );

        // The failure policy applies: ToolCalling blocks fail-safe; nothing throws into the run.
        Assert.IsType<ToolCallingResult.Block>(result.Value);
        Assert.Contains(workspace.Log.Errors, entry =>
            entry.Contains("was refused", StringComparison.Ordinal)
        );
        Assert.Equal(RemoteExtensionState.Refused, loader.RemoteStatus("remote-sample")!.State);
    }

    [Fact]
    public async Task The_trust_digest_covers_the_extension_directory_contents()
    {
        using var workspace = new RemoteTestWorkspace(project: true);
        ExtensionLoader first = workspace.CreateLoader();
        var prompt = new ScriptedTrustPrompt(true);

        await workspace.LoadAsync(first, prompt);

        Assert.Equal(1, prompt.Calls);

        // Swapping a bundled file after trust re-prompts (the digest covers the directory).
        workspace.RewriteBundledScript("// changed");

        ExtensionLoader second = workspace.CreateLoader();
        var secondPrompt = new ScriptedTrustPrompt(true);
        await workspace.LoadAsync(second, secondPrompt);

        Assert.Equal(1, secondPrompt.Calls);
        Assert.True(secondPrompt.Approvals >= 1);
    }

    [Fact]
    public async Task A_manual_restart_through_the_loader_starts_a_fresh_child()
    {
        using var workspace = new RemoteTestWorkspace();
        ExtensionLoader loader = workspace.CreateLoader();
        await workspace.LoadAsync(loader);
        await loader.Hooks.RunToolCallingAsync(
            new ToolCallingPayload(
                "r1",
                "c1",
                "read",
                JsonDocument.Parse("{}").RootElement.Clone()
            ),
            Ct
        );
        Assert.Equal(1, workspace.Child.StartCount);

        Assert.True(await loader.RestartRemoteAsync("remote-sample", Ct));

        Assert.Equal(2, workspace.Child.StartCount);
        Assert.False(await loader.RestartRemoteAsync("absent", Ct));
    }

    [Fact]
    public async Task An_observer_receives_the_json_line_with_the_host_priced_cost()
    {
        using var workspace = new RemoteTestWorkspace(events: ["usage_updated"]);
        // The host holds the catalog and prices the record; the wire package cannot, so the
        // composition hands it this delegate and the observer's params are the --json line.
        ExtensionLoader loader = workspace.CreateLoader(costUsd: _ => 0.25m);
        await workspace.LoadAsync(loader);
        var usage = new UsageUpdated(
            "run_1",
            new Microsoft.Extensions.AI.UsageDetails { InputTokenCount = 1000 }
        );

        loader.EventStream.Publish(usage);

        await WaitAsync(
            () => workspace.Child.ObservedEventKinds().Contains("usage_updated"),
            "the usage event"
        );
        (string _, JsonElement? parameters) = workspace.Child.Notifications.Single(notification =>
            notification.Method == "event/usage_updated"
        );
        Assert.Equal(0.25m, parameters!.Value.GetProperty("costUsd").GetDecimal());
    }

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 750; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        Assert.Fail($"condition not reached: {what}");
    }
}
