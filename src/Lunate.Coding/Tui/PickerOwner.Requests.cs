using Lunate.Tui;

namespace Lunate.Coding;

internal sealed partial class PickerOwner
{
    private enum Operation
    {
        Open,
        Move,
        AppendFilter,
        BackspaceFilter,
        ClearFilter,
        Dismiss,
        Navigate,
        Confirm,
    }

    private sealed class Request
    {
        public Operation Operation { get; init; }
        public PickerKind Kind { get; init; }
        public string TitlePrefix { get; init; } = string.Empty;
        public List<string> Labels { get; init; } = [];
        public List<string> Values { get; init; } = [];
        public int Selected { get; init; }
        public bool Menu { get; init; }
        public TaskCompletionSource<string?>? Pick { get; init; }
        public SelectListModel? Expected { get; init; }
        public int Delta { get; init; }
        public string Text { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;

        public TaskCompletionSource<PickerConfirmation?> Done { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Opens <paramref name="kind"/>; <paramref name="pick"/> is the wizard pick the owner
    /// completes when this picker is confirmed, navigated or dismissed.</summary>
    internal ValueTask OpenAsync(
        PickerKind kind,
        string titlePrefix,
        List<string> labels,
        List<string> values,
        int selected,
        bool menu,
        TaskCompletionSource<string?>? pick = null
    ) =>
        SendAsync(
            new Request
            {
                Operation = Operation.Open,
                Kind = kind,
                TitlePrefix = titlePrefix,
                Labels = labels,
                Values = values,
                Selected = selected,
                Menu = menu,
                Pick = pick,
            }
        );

    internal ValueTask MoveAsync(SelectListModel expected, int delta) =>
        SendAsync(
            new Request
            {
                Operation = Operation.Move,
                Expected = expected,
                Delta = delta,
            }
        );

    internal ValueTask AppendFilterAsync(SelectListModel expected, string text) =>
        SendAsync(
            new Request
            {
                Operation = Operation.AppendFilter,
                Expected = expected,
                Text = text,
            }
        );

    internal ValueTask BackspaceFilterAsync(SelectListModel expected) =>
        SendAsync(new Request { Operation = Operation.BackspaceFilter, Expected = expected });

    internal ValueTask ClearFilterAsync(SelectListModel expected) =>
        SendAsync(new Request { Operation = Operation.ClearFilter, Expected = expected });

    internal ValueTask DismissAsync(SelectListModel expected) =>
        SendAsync(new Request { Operation = Operation.Dismiss, Expected = expected });

    internal ValueTask NavigateAsync(SelectListModel expected, string value) =>
        SendAsync(
            new Request
            {
                Operation = Operation.Navigate,
                Expected = expected,
                Value = value,
            }
        );

    internal async ValueTask<PickerConfirmation?> ConfirmAsync(SelectListModel expected)
    {
        var request = new Request { Operation = Operation.Confirm, Expected = expected };
        if (!_requests.Writer.TryWrite(request))
        {
            return null;
        }

        return await request.Done.Task.ConfigureAwait(false);
    }

    private ValueTask SendAsync(Request request) =>
        _requests.Writer.TryWrite(request)
            ? new ValueTask(request.Done.Task)
            : ValueTask.CompletedTask;
}
