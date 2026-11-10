using System.Runtime.CompilerServices;
using System.Text.Json;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

public sealed class RunLimitsCodingTests
{
    private const string PricedModelJson =
        """{"schemaVersion":1,"models":[{"id":"priced-run","provider":"openai","contextWindow":1000,"supportsTools":true,"pricing":{"input":1000000,"output":0}}]}""";

    [Fact]
    public async Task A_cost_limit_run_exits_three_and_names_the_spend()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("models.json"), PricedModelJson);
        var client = new ScriptedChatClient().Enqueue(
            Scripts.Usage(1, 0),
            Scripts.Text("Done"),
            Scripts.Stop()
        );
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Go",
            Json = true,
            Launch = new LaunchOptions { Model = "priced-run", MaxCostUsd = 1m },
        };

        (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(3, exitCode);
        Assert.Equal(
            "lunate: cost limit reached (limit $1.00, spent $1.00)",
            errors.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1]
        );
        List<JsonElement> lines = PrintModeTestSupport.ParseLines(output);
        JsonElement limit = Assert.Single(
            lines,
            line => PrintModeTestSupport.TypeOf(line) == "cost_limit_reached"
        );
        Assert.Equal(1m, limit.GetProperty("maxCostUsd").GetDecimal());
        Assert.Equal(1m, limit.GetProperty("spendUsd").GetDecimal());
        Assert.Equal("cost_limit", lines[^1].GetProperty("stopReason").GetString());
    }

    [Fact]
    public async Task A_time_limit_run_exits_four_and_names_the_elapsed_time()
    {
        using var temp = new TempDirectory();
        var clock = new ManualTimeProvider();
        var client = new AdvancingChatClient(
            clock,
            TimeSpan.FromTicks(5 * TimeSpan.TicksPerSecond)
        );
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Go",
            Json = true,
            TimeProvider = clock,
            Launch = new LaunchOptions
            {
                MaxTime = TimeSpan.FromTicks(5 * TimeSpan.TicksPerSecond),
            },
        };

        (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(4, exitCode);
        Assert.Equal(
            "lunate: time limit reached (limit 5s, elapsed 5s)",
            errors.Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1]
        );
        List<JsonElement> lines = PrintModeTestSupport.ParseLines(output);
        JsonElement limit = Assert.Single(
            lines,
            line => PrintModeTestSupport.TypeOf(line) == "time_limit_reached"
        );
        Assert.Equal(5d, limit.GetProperty("maxTimeSeconds").GetDouble());
        Assert.Equal(5d, limit.GetProperty("elapsedSeconds").GetDouble());
        Assert.Equal("time_limit", lines[^1].GetProperty("stopReason").GetString());
    }

    [Fact]
    public async Task An_unpriced_model_refuses_a_cost_limit_before_any_request()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("models.json"),
            """{"schemaVersion":1,"models":[{"id":"unpriced-run","provider":"openai","contextWindow":1000,"supportsTools":true}]}"""
        );
        var factory = new FakeChatClientFactory(
            new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop())
        );
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(temp, factory) with
        {
            Prompt = "Go",
            Launch = new LaunchOptions { Model = "unpriced-run", MaxCostUsd = 1m },
        };

        (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Contains("unpriced-run", errors, StringComparison.Ordinal);
        Assert.Contains("--allow-unpriced", errors, StringComparison.Ordinal);
        Assert.Null(factory.CreatedModel);
    }

    [Fact]
    public async Task Allow_unpriced_starts_the_run()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("models.json"),
            """{"schemaVersion":1,"models":[{"id":"unpriced-run","provider":"openai","contextWindow":1000,"supportsTools":true}]}"""
        );
        var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Go",
            Launch = new LaunchOptions
            {
                Model = "unpriced-run",
                MaxCostUsd = 1m,
                AllowUnpriced = true,
            },
        };

        (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(0, exitCode);
        Assert.Equal("ok", output);
        Assert.Empty(errors);
    }

    private sealed class AdvancingChatClient(ManualTimeProvider clock, TimeSpan advance)
        : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new ChatResponse());

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            clock.Advance(advance);
            cancellationToken.ThrowIfCancellationRequested();
            yield return Scripts.Text("Done");
            yield return Scripts.Stop();
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}

/// <summary>A controllable monotonic clock whose timers fire when the test advances it.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private long _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _timestamp;

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = new ManualTimer(callback, state);
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            timer.DueAt = _timestamp + dueTime.Ticks;
            _timers.Add(timer);
        }

        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        _timestamp += delta.Ticks;
        foreach (ManualTimer timer in _timers.ToArray())
        {
            if (timer.DueAt > _timestamp)
            {
                continue;
            }

            _timers.Remove(timer);
            timer.Fire();
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public long DueAt { get; set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
