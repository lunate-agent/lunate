namespace Lunate.Coding.Tests;

/// <summary>
/// #710: a wait whose condition advances the host's fake clock must not take its simulated time
/// from the number of wall polls. A condition that needs 1,000 frames reaches them in the same wall
/// time at every tick, however few polls that tick allows.
/// </summary>
public sealed class WaitClockTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task A_clock_stepping_wait_reaches_the_same_simulated_deadline_at_any_tick(
        int pollIntervalMilliseconds
    )
    {
        using var host = new InteractiveSessionHost(
            pollInterval: TimeSpan.FromMilliseconds(pollIntervalMilliseconds)
        );
        DateTimeOffset startedAt = host.Scheduler.Now;
        TimeSpan simulatedDeadline = WaitBudgets.SimulatedFrameStep * 1_000;
        int step = (int)WaitBudgets.SimulatedFrameStep.TotalMilliseconds;

        await host.WaitUntilAsync(
            () =>
            {
                host.Advance(step);
                return host.Scheduler.Now - startedAt >= simulatedDeadline;
            },
            WaitBudgets.InProcess
        );

        Assert.True(host.Scheduler.Now - startedAt >= simulatedDeadline);
    }
}
