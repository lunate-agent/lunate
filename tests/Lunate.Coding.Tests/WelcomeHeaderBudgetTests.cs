using System.Diagnostics;
using System.Globalization;

namespace Lunate.Coding.Tests;

/// <summary>
/// The welcome header's startup budget (design D6, trait <c>Category=Perf</c>, excluded from the
/// main passes and run by the gate's own perf step): the first frame reaches the header well inside
/// the budget, so the render never becomes the reason an interactive start feels slow. The check is
/// deliberately generous — it fails on a header that started doing I/O, not on a slow runner.
/// <para>
/// The gate is the fastest of several first frames, the statistic <c>scripts/perf.sh</c> reads
/// (ADR-0005 addendum): a cold test process JITs the whole interactive stack, so its very first
/// frame measures the runner's tier-0 compilation as much as the header (measured 260–275 ms cold
/// against a 11–15 ms floor once the process is warm on the implementation machine, the same on
/// .NET 10 and .NET 11, and 1.5–1.7 s for a single cold frame on a starved CI runner whose verify
/// step took 4× its usual wall time). Interference only ever adds time, so the fastest run
/// estimates what the header costs when it is not being starved, and a header that gained I/O
/// moves every run including that one. The budget is unchanged — the measurement moved from the
/// runner's JIT to the header.
/// </para>
/// </summary>
public sealed class WelcomeHeaderBudgetTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    /// <summary>How many first frames are sampled; the fastest is the gate.</summary>
    private const int Runs = 5;

    [Fact]
    [Trait("Category", "Perf")]
    public async Task The_first_frame_header_stays_within_the_startup_budget()
    {
        var measured = new List<TimeSpan>(Runs);
        for (int run = 0; run < Runs; run++)
        {
            measured.Add(await FirstFrameAsync());
        }

        TimeSpan floor = measured.Min();
        TimeSpan median = measured.Order().ElementAt(Runs / 2);
        string readings = string.Join(", ", measured.Select(Format));
        Assert.True(
            floor < Budget,
            FormattableString.Invariant(
                $"the fastest of {Runs} welcome headers took {Format(floor)} (median {Format(median)}, runs: {readings}), above the {Format(Budget)} budget"
            )
        );
    }

    /// <summary>
    /// Starts one session and returns the time its first frame needed to reach the header: the
    /// clock stops on the same event the assertion is about, the header committed to scrollback.
    /// </summary>
    private static async Task<TimeSpan> FirstFrameAsync()
    {
        using var host = new InteractiveSessionHost(environment: name =>
            name switch
            {
                "LUNATE_UNICODE" => "1",
                "OPENAI_API_KEY" => "test-key",
                _ => PrintModeTestSupport.Environment()(name),
            }
        );
        var stopwatch = Stopwatch.StartNew();
        Task run = host.RunAsync();
        await host.WaitForScrollbackAsync($"lunate  v{ProductVersion.Build}");
        stopwatch.Stop();

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
        return stopwatch.Elapsed;
    }

    private static string Format(TimeSpan elapsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalMilliseconds:0.0} ms");
}
