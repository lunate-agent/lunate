using System.Globalization;
using System.Threading.Channels;
using Lunate.Tui;

namespace Lunate.Coding;

/// <summary>The kind of picker currently open; it decides filtering and what a confirmed row means.</summary>
internal enum PickerKind
{
    Model,
    Session,
    AuthProvider,
    AuthVerify,
    WizardProvider,
    WizardChoice,
    WizardModel,
}

/// <summary>The picker's state as the key pump reads it; immutable, so a reader never sees a half transition.</summary>
internal sealed record PickerSnapshot(SelectListModel? Picker, PickerKind Kind, string Filter)
{
    public bool IsOpen => Picker is not null;

    public bool FiltersOn =>
        Kind
            is PickerKind.Model
                or PickerKind.WizardModel
                or PickerKind.WizardProvider
                or PickerKind.WizardChoice;
}

/// <summary>What a confirmed picker row means, answered only after the owner has dismissed the picker.</summary>
internal sealed record PickerConfirmation(PickerKind Kind, string Value);

/// <summary>
/// The one owner of the picker lifecycle: open, move, filter, confirm and dismiss are messages on
/// one channel, applied by one consumer, so no two transitions can interleave. It is the only
/// writer of the picker state and the only poster of <c>SetPicker(...)</c>; every other thread
/// requests a transition and the owner applies requests in arrival order.
/// <para>
/// A request may target the picker it was routed on. If another transition already replaced that
/// picker, the request is stale and is dropped — a dismissal of picker A can never clear picker B,
/// no matter how the two requests were ordered before they reached the queue.
/// </para>
/// </summary>
internal sealed partial class PickerOwner
{
    private readonly Channel<Request> _requests = Channel.CreateUnbounded<Request>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        }
    );
    private readonly Func<string> _currentModelId;
    private readonly Action<SelectListModel?> _post;
    private readonly Task _loop;

    // Owned by the consumer loop; no other thread reads or writes these.
    private SelectListModel? _picker;
    private PickerKind _kind;
    private List<string> _labels = [];
    private List<string> _values = [];
    private List<string> _visibleValues = [];
    private string _titlePrefix = string.Empty;
    private string _filter = string.Empty;
    private TaskCompletionSource<string?>? _pick;

    // Published by the loop, read by the key pump and the wizard: one immutable snapshot at a time.
    private PickerSnapshot _snapshot = new(null, default, string.Empty);

    internal PickerOwner(Func<string> currentModelId, Action<SelectListModel?> post)
    {
        _currentModelId = currentModelId;
        _post = post;
        _loop = Task.Run(LoopAsync);
    }

    internal PickerSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Stops the owner after the requests already queued have been applied.</summary>
    internal Task StopAsync()
    {
        _requests.Writer.TryComplete();
        return _loop;
    }

    private async Task LoopAsync()
    {
        await foreach (Request request in _requests.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Apply(request);
                request.Done.TrySetResult();
            }
            catch (Exception exception)
            {
                request.Done.TrySetException(exception);
                request.Confirmed.TrySetException(exception);
            }
        }
    }

    private void Apply(Request request)
    {
        switch (request.Operation)
        {
            case Operation.Open:
                Open(request);
                break;
            case Operation.Move:
                Move(request);
                break;
            case Operation.AppendFilter:
                AppendFilter(request);
                break;
            case Operation.BackspaceFilter:
                BackspaceFilter(request);
                break;
            case Operation.ClearFilter:
                ClearFilter(request);
                break;
            case Operation.Dismiss:
                Dismiss(request);
                break;
            case Operation.Confirm:
                Confirm(request);
                break;
        }
    }

    private void Open(Request request)
    {
        // A picker replaced while its pick is pending is gone: the walk must not await it forever.
        _pick?.TrySetResult(null);
        _kind = request.Kind;
        _titlePrefix = request.TitlePrefix;
        _labels = request.Labels;
        _values = request.Values;
        _filter = string.Empty;
        _pick = request.Pick;
        Show(keepValue: null, fallback: request.Selected);
    }

    private void Move(Request request)
    {
        if (!IsCurrent(request.Expected))
        {
            return;
        }

        SelectListModel moved = _picker!.Move(request.Delta);
        Set(moved with { Title = Title(moved.Selected, moved.Items.Count) });
    }

    private void AppendFilter(Request request)
    {
        if (!IsCurrent(request.Expected) || !FiltersOn())
        {
            return;
        }

        _filter += request.Text;
        Show(SelectedValue(), fallback: 0);
    }

    private void BackspaceFilter(Request request)
    {
        if (!IsCurrent(request.Expected) || _kind is not PickerKind.Model || _filter.Length == 0)
        {
            return;
        }

        _filter = _filter[..^1];
        Show(SelectedValue(), fallback: 0);
    }

    private void ClearFilter(Request request)
    {
        if (!IsCurrent(request.Expected) || !FiltersOn() || _filter.Length == 0)
        {
            return;
        }

        _filter = string.Empty;
        Show(SelectedValue(), fallback: 0);
    }

    private void Dismiss(Request request)
    {
        if (!IsCurrent(request.Expected))
        {
            return;
        }

        TaskCompletionSource<string?>? pick = _pick;
        Clear();
        pick?.TrySetResult(null);
    }

    private void Confirm(Request request)
    {
        if (!IsCurrent(request.Expected) || SelectedValue() is not { } value)
        {
            // Without a current picker or a selected row Enter is a no-op: the picker stays open.
            request.Confirmed.TrySetResult(null);
            return;
        }

        PickerKind kind = _kind;
        TaskCompletionSource<string?>? pick = _pick;
        Clear();
        pick?.TrySetResult(value);
        request.Confirmed.TrySetResult(new PickerConfirmation(kind, value));
    }

    private bool IsCurrent(SelectListModel? expected) =>
        _picker is not null && (expected is null || ReferenceEquals(_picker, expected));

    private void Clear()
    {
        _picker = null;
        _pick = null;
        _titlePrefix = string.Empty;
        _labels = [];
        _values = [];
        _visibleValues = [];
        _filter = string.Empty;
        Publish();
    }

    private void Show(string? keepValue, int fallback)
    {
        List<string> labels = [];
        List<string> values = [];
        for (var index = 0; index < _labels.Count; index++)
        {
            if (!Matches(_labels[index]))
            {
                continue;
            }

            labels.Add(Label(index));
            values.Add(_values[index]);
        }

        _visibleValues = values;
        int selected = keepValue is null ? fallback : values.IndexOf(keepValue);
        if (selected < 0)
        {
            selected = fallback;
        }

        selected = Math.Clamp(selected, 0, Math.Max(labels.Count - 1, 0));
        Set(new SelectListModel(Title(selected, labels.Count), labels, selected));
    }

    private void Set(SelectListModel picker)
    {
        _picker = picker;
        Publish();
    }

    private void Publish()
    {
        SelectListModel? picker = _picker;
        Volatile.Write(ref _snapshot, new PickerSnapshot(picker, _kind, _filter));
        _post(picker);
    }

    private bool FiltersOn() =>
        _kind
            is PickerKind.Model
                or PickerKind.WizardModel
                or PickerKind.WizardProvider
                or PickerKind.WizardChoice;

    private bool Matches(string label) =>
        !FiltersOn()
        || _filter.Length == 0
        || label.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private string Label(int index) =>
        _kind is PickerKind.Model
        && string.Equals(_values[index], _currentModelId(), StringComparison.Ordinal)
            ? "* " + _labels[index]
            : _labels[index];

    private string Title(int selected, int count)
    {
        if (_kind is not PickerKind.Model)
        {
            return _titlePrefix;
        }

        string position =
            count == 0 ? "0 of 0" : FormattableString.Invariant($"{selected + 1} of {count}");
        string title = _titlePrefix + " — " + position;
        return _filter.Length == 0 ? title : title + " — filter: " + _filter;
    }

    private string? SelectedValue() =>
        _picker is { Selected: >= 0 } picker && picker.Selected < _visibleValues.Count
            ? _visibleValues[picker.Selected]
            : null;
}
