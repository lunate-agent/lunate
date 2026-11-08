namespace Lunate.Coding.Tests;

/// <summary>
/// What a timed-out wait says about itself (#660). The issue's occurrences each cost an investigation
/// because the failure was a bare <c>TimeoutException</c>: no condition, no poll count, no process
/// tree. These tests drive a wait to its own expiry and pin the evidence, so the next occurrence
/// arrives with it and the shape cannot be lost again without a red test.
/// </summary>
public sealed class WaitDiagnosisTests
{
    [Fact]
    public async Task An_expired_wait_names_the_condition_it_polled()
    {
        using var host = new InteractiveSessionHost();

        TimeoutException failure = await Assert.ThrowsAsync<TimeoutException>(() =>
            host.WaitUntilAsync(() => host.Session.IsRunning, WaitBudgets.WaitExpiry)
        );

        Assert.Contains("() => host.Session.IsRunning", failure.Message, StringComparison.Ordinal);
        // The budget the wait applied, formatted invariantly: a message that changes with the
        // machine's culture is not evidence another leg can compare against.
        Assert.Contains("within 0.25 seconds", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_expired_wait_reports_its_poll_count_and_longest_gap()
    {
        using var host = new InteractiveSessionHost();

        TimeoutException failure = await Assert.ThrowsAsync<TimeoutException>(() =>
            host.WaitUntilAsync(() => false, WaitBudgets.WaitExpiry)
        );

        Assert.Contains("polls=", failure.Message, StringComparison.Ordinal);
        Assert.Contains("longestGap=", failure.Message, StringComparison.Ordinal);
        Assert.Contains("threadPool:", failure.Message, StringComparison.Ordinal);
        // The poll loop has to have observed the condition more than once: a wait that expires
        // without polling cannot distinguish a hang from a starved thread.
        int polls = int.Parse(
            failure.Message.Split("polls=")[1].Split(',')[0],
            System.Globalization.CultureInfo.InvariantCulture
        );
        Assert.True(polls > 1, $"the wait expired after {polls} poll(s): {failure.Message}");
    }

    [Fact]
    public async Task An_expired_wait_reports_the_session_state_its_condition_reads()
    {
        using var host = new InteractiveSessionHost();

        TimeoutException failure = await Assert.ThrowsAsync<TimeoutException>(() =>
            host.WaitUntilAsync(() => host.Session.IsComposed, WaitBudgets.WaitExpiry)
        );

        // `IsComposed` is the condition in most call sites of this helper, including the
        // `SetupWizardStepPins` occurrence this issue names, and it was the one field the snapshot
        // did not carry before #660.
        foreach (
            string field in new[]
            {
                "composed=",
                "running=",
                "input=",
                "cursor=",
                "followUps=",
                "transcript=",
                "alternateScreen=",
                "scheduledVt=",
            }
        )
        {
            Assert.Contains(field, failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_expired_wait_reports_the_process_tree_including_its_own_pid()
    {
        using var host = new InteractiveSessionHost();

        TimeoutException failure = await Assert.ThrowsAsync<TimeoutException>(() =>
            host.WaitUntilAsync(() => false, WaitBudgets.WaitExpiry)
        );

        Assert.Contains("processTree:", failure.Message, StringComparison.Ordinal);
        Assert.Contains("(this test process)", failure.Message, StringComparison.Ordinal);
        Assert.Contains(
            FormattableString.Invariant($"self={Environment.ProcessId}"),
            failure.Message,
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// The parser and the walk, without a live <c>ps</c>: one line per descendant, the root marked,
    /// a command that carries spaces kept whole, and an unrelated process left out.
    /// </summary>
    [Fact]
    public void A_process_table_becomes_one_line_per_descendant()
    {
        const string table = """

              10     1    10 Ss   -   /usr/bin/parent
              11    10    10 S    -   /usr/bin/child with spaces
              12    11    10 R+   -   /usr/bin/grandchild
              99     1     1 S    -   /usr/bin/unrelated

            """;

        IReadOnlyList<string> lines = ProcessTable.Describe(table, 10, "/bin/ps");

        Assert.Contains(
            lines,
            line =>
                line.Contains("pid=10 ", StringComparison.Ordinal)
                && line.Contains("(this test process)", StringComparison.Ordinal)
        );
        Assert.Contains(
            lines,
            line =>
                line.Contains("pid=11 ", StringComparison.Ordinal)
                && line.Contains("comm=/usr/bin/child with spaces", StringComparison.Ordinal)
        );
        Assert.Contains(
            lines,
            line =>
                line.Contains("pid=12 ", StringComparison.Ordinal)
                && line.Contains("stat=R+", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(lines, line => line.Contains("pid=99 ", StringComparison.Ordinal));
    }

    /// <summary>A table that names no process is reported, not parsed into a misleading silence.</summary>
    [Fact]
    public void An_empty_process_table_is_reported_as_unavailable()
    {
        IReadOnlyList<string> lines = ProcessTable.Describe(string.Empty, 10, "/bin/ps");

        Assert.Contains(lines, line => line.StartsWith("unavailable:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The unnamed budget is the cross-turn ceiling and stays it: #660 forbids hiding a hang behind a
    /// longer wait, and the diagnosis is what makes the next occurrence answerable instead.
    /// </summary>
    [Fact]
    public void The_unnamed_wait_budget_is_still_the_cross_turn_ceiling()
    {
        Assert.Equal(WaitBudgets.CrossTurn, InteractiveSessionHost.DefaultWait);
    }
}
