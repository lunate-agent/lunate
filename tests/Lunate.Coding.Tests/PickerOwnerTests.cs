using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The picker lifecycle stated as message order (#735). Both former races were expressions of the
/// same shape — a dismissal of picker A running inside the transition that installed picker B —
/// and are now requests one owner applies in order, with no held threads or sleeps.
/// </summary>
public sealed class PickerOwnerTests
{
    /// <summary>
    /// #731: the walk had already posted the next picker when the pump's dismissal of the previous
    /// one arrived. The dismissal targets the picker it was read against, so the newer picker and
    /// its pick survive: confirming it still answers the walk.
    /// </summary>
    [Fact]
    public async Task A_dismissal_of_a_replaced_picker_leaves_the_new_picker_open()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            var picked = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            SelectListModel first = await OpenAsync(owner, "First");
            SelectListModel second = await OpenAsync(owner, "Second", pick: picked);

            await owner.DismissAsync(first);

            Assert.Same(second, owner.Snapshot.Picker);
            Assert.Equal(["First", "Second"], posted.Select(picker => picker?.Title));
            Assert.Equal("one", (await owner.ConfirmAsync(second))?.Value);
            Assert.Equal("one", await picked.Task);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    /// <summary>
    /// #711: the pump's dismissal was committed before the walk opened the next picker, so the
    /// dismissal came first and cannot cancel the pick the walk is waiting on.
    /// </summary>
    [Fact]
    public async Task A_dismissal_before_the_next_open_does_not_cancel_the_next_pick()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            SelectListModel first = await OpenAsync(owner, "First");
            await owner.DismissAsync(first);
            Assert.False(owner.Snapshot.IsOpen);
            Assert.Null(posted[^1]);

            var picked = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            SelectListModel second = await OpenAsync(owner, "Second", pick: picked);
            Assert.Equal("one", (await owner.ConfirmAsync(second))?.Value);

            Assert.Equal("one", await picked.Task);
            Assert.False(owner.Snapshot.IsOpen);
            Assert.Equal(["First", null, "Second", null], posted.Select(picker => picker?.Title));
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    /// <summary>
    /// A confirmed pick completes only after the owner has dismissed its picker, so the walk's
    /// continuation (which opens the next step) cannot race the dismissal that completed it.
    /// </summary>
    [Fact]
    public async Task A_confirmed_pick_opens_the_next_picker_after_the_dismissal()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            var picked = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            SelectListModel first = await OpenAsync(owner, "First", pick: picked);
            var nextOpened = new TaskCompletionSource<SelectListModel?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            _ = Task.Run(
                async () =>
                {
                    string? value = await picked.Task.ConfigureAwait(false);
                    Assert.Equal("one", value);
                    await owner
                        .OpenAsync(PickerKind.WizardChoice, "Next", ["next"], ["next"], 0, false)
                        .ConfigureAwait(false);
                    nextOpened.TrySetResult(owner.Snapshot.Picker);
                },
                TestContext.Current.CancellationToken
            );

            PickerConfirmation? confirmed = await owner.ConfirmAsync(first);

            Assert.Equal("one", confirmed?.Value);
            SelectListModel next = Assert.IsType<SelectListModel>(
                await nextOpened.Task.WaitAsync(
                    WaitBudgets.InProcess,
                    TestContext.Current.CancellationToken
                )
            );
            Assert.Equal("Next", next.Title);
            Assert.Same(next, owner.Snapshot.Picker);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    /// <summary>A wizard verb closes the picker and completes its pick with the verb, never with a
    /// row; a picker without a pending pick ignores the verb and stays open.</summary>
    [Fact]
    public async Task A_navigation_verb_completes_the_pick_and_closes_the_picker()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            var picked = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            SelectListModel menu = await OpenAsync(owner, "Menu", pick: picked, menu: true);

            await owner.NavigateAsync(menu, InteractiveSession.WizardBackValue);

            Assert.False(owner.Snapshot.IsOpen);
            Assert.Null(posted[^1]);
            Assert.Equal(InteractiveSession.WizardBackValue, await picked.Task);

            // A picker with no pending pick is not a wizard pick: the verb leaves it open.
            SelectListModel plain = await OpenAsync(owner, "Plain");
            await owner.NavigateAsync(plain, InteractiveSession.WizardBackValue);
            Assert.Same(plain, owner.Snapshot.Picker);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    /// <summary>A request aimed at a picker the owner already replaced changes nothing (#731): a
    /// move, a filter or a confirmation of the old picker can never touch the new one.</summary>
    [Fact]
    public async Task A_request_for_a_replaced_picker_changes_nothing()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            SelectListModel first = await OpenAsync(owner, "First", labels: ["one", "two"]);
            SelectListModel second = await OpenAsync(owner, "Second", labels: ["one", "two"]);

            await owner.MoveAsync(first, 1);
            await owner.AppendFilterAsync(first, "x");
            PickerConfirmation? confirmed = await owner.ConfirmAsync(first);

            Assert.Null(confirmed);
            Assert.Same(second, owner.Snapshot.Picker);
            Assert.Equal(0, owner.Snapshot.Picker?.Selected);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    /// <summary>
    /// A pick cannot outlive its picker: when an open replaces a picker whose pick is still
    /// pending, the owner completes that pick as dismissed instead of leaving the walk on it.
    /// </summary>
    [Fact]
    public async Task An_open_that_replaces_a_pending_picker_completes_its_pick_as_dismissed()
    {
        var owner = new PickerOwner(() => "model", _ => { });
        try
        {
            var stale = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            await OpenAsync(owner, "First", pick: stale);
            await OpenAsync(owner, "Second");

            Assert.Null(
                await stale.Task.WaitAsync(
                    WaitBudgets.InProcess,
                    TestContext.Current.CancellationToken
                )
            );
            Assert.Equal("Second", owner.Snapshot.Picker?.Title);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    /// <summary>
    /// A short wizard list keeps bare <c>b</c>/<c>f</c> for the walk's verbs; only a list above the
    /// threshold filters as text is typed, and the model picker always filters (spec: The walk is
    /// navigable).
    /// </summary>
    [Fact]
    public async Task Only_a_long_wizard_list_filters_but_the_model_picker_always_does()
    {
        var owner = new PickerOwner(() => "model", _ => { });
        try
        {
            string[] rows =
            [
                .. Enumerable.Range(0, PickerOwner.FilterThreshold).Select(i => $"r{i}"),
            ];
            SelectListModel wizardShort = await OpenAsync(
                owner,
                "Wizard",
                labels: rows,
                kind: PickerKind.WizardChoice
            );
            Assert.False(owner.Snapshot.FiltersOn);

            await owner.AppendFilterAsync(wizardShort, "b");
            Assert.Equal(string.Empty, owner.Snapshot.Filter);

            string[] longRows =
            [
                .. Enumerable.Range(0, PickerOwner.FilterThreshold + 1).Select(i => $"r{i}"),
            ];
            SelectListModel wizardLong = await OpenAsync(
                owner,
                "Wizard",
                labels: longRows,
                kind: PickerKind.WizardChoice
            );
            Assert.True(owner.Snapshot.FiltersOn);

            await owner.AppendFilterAsync(wizardLong, "r1");
            Assert.Equal("r1", owner.Snapshot.Filter);

            SelectListModel model = await OpenAsync(
                owner,
                "Model",
                labels: ["one"],
                kind: PickerKind.Model
            );
            Assert.True(owner.Snapshot.FiltersOn);

            await owner.AppendFilterAsync(model, "on");
            Assert.Equal("on", owner.Snapshot.Filter);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    private static async Task<SelectListModel> OpenAsync(
        PickerOwner owner,
        string title,
        string[]? labels = null,
        PickerKind kind = PickerKind.WizardChoice,
        TaskCompletionSource<string?>? pick = null,
        bool menu = false
    )
    {
        List<string> values = labels is null ? ["one"] : [.. labels];
        await owner.OpenAsync(kind, title, [.. values], values, 0, menu, pick);
        return Assert.IsType<SelectListModel>(owner.Snapshot.Picker);
    }
}
