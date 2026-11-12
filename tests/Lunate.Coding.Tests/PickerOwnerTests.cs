using Lunate.Coding;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The picker lifecycle as message order (#735). The two races were fixed by guarding a mutation
/// on another thread; these tests state the same scenarios as requests the owner applies in order,
/// with no held threads or sleeps.
/// </summary>
public sealed class PickerOwnerTests
{
    [Fact]
    public async Task A_dismissal_of_a_replaced_picker_leaves_the_new_picker_open()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            SelectListModel first = await OpenAsync(owner, "First");
            SelectListModel second = await OpenAsync(owner, "Second");

            // The walk's open was already queued when the pump's dismissal arrived (#731).
            await owner.DismissAsync(first);

            Assert.Same(second, owner.Snapshot.Picker);
            Assert.Equal("Second", posted[^1]?.Title);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

    [Fact]
    public async Task A_dismissal_before_the_next_open_does_not_cancel_the_next_picker()
    {
        var posted = new List<SelectListModel?>();
        var owner = new PickerOwner(() => "model", posted.Add);
        try
        {
            SelectListModel first = await OpenAsync(owner, "First");
            await owner.DismissAsync(first);
            Assert.False(owner.Snapshot.IsOpen);

            SelectListModel second = await OpenAsync(owner, "Second");

            Assert.Same(second, owner.Snapshot.Picker);
            Assert.Equal("Second", posted[^1]?.Title);
        }
        finally
        {
            await owner.StopAsync();
        }
    }

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
                        .OpenAsync(PickerKind.WizardChoice, "Next", ["next"], ["next"], 0)
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

    private static async Task<SelectListModel> OpenAsync(
        PickerOwner owner,
        string title,
        TaskCompletionSource<string?>? pick = null
    )
    {
        await owner.OpenAsync(PickerKind.WizardChoice, title, ["one"], ["one"], 0, pick);
        return Assert.IsType<SelectListModel>(owner.Snapshot.Picker);
    }
}
