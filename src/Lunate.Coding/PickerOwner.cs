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

/// <summary>
/// The one owner of the picker lifecycle: open, move, filter, confirm, navigate and dismiss are
/// messages on one channel, applied by one consumer, so no two transitions can interleave. It is
/// the only writer of the picker state and the only poster of <c>SetPicker(...)</c>; every other
/// thread requests a transition and the owner applies requests in arrival order.
/// <para>
/// A request may target the picker it was routed on. If another transition already replaced that
/// picker the request is stale and is dropped — a dismissal of picker A can never clear picker B,
/// however the two requests were ordered before they reached the queue.
/// </para>
/// </summary>
internal sealed partial class PickerOwner
{
    /// <summary>The row count above which a wizard list filters; a short list keeps bare
    /// <c>b</c> and <c>f</c> free for the wizard's back and forward verbs.</summary>
    internal const int FilterThreshold = 15;

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
    private bool _menu;
    private TaskCompletionSource<string?>? _pick;

    // Published by the loop, read by the key pump: one immutable snapshot at a time.
    private PickerSnapshot _snapshot = new(null, default, string.Empty, false, false);

    /// <param name="currentModelId">The active model, read only while a row label is built so the
    /// model picker can keep its <c>* </c> marker. A string reference is read atomically; it is
    /// display input, never a transition.</param>
    /// <param name="post">The live area's picker slot: the one place the owner publishes to.</param>
    internal PickerOwner(Func<string> currentModelId, Action<SelectListModel?> post)
    {
        ArgumentNullException.ThrowIfNull(currentModelId);
        ArgumentNullException.ThrowIfNull(post);
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
                request.Done.TrySetResult(Apply(request));
            }
            catch (Exception exception)
            {
                request.Done.TrySetException(exception);
            }
        }
    }

    private PickerConfirmation? Apply(Request request)
    {
        switch (request.Operation)
        {
            case Operation.Open:
                Open(request);
                return null;
            case Operation.Move:
                Move(request);
                return null;
            case Operation.AppendFilter:
                AppendFilter(request);
                return null;
            case Operation.BackspaceFilter:
                BackspaceFilter(request);
                return null;
            case Operation.ClearFilter:
                ClearFilter(request);
                return null;
            case Operation.Dismiss:
                Dismiss(request);
                return null;
            case Operation.Navigate:
                Navigate(request);
                return null;
            case Operation.Confirm:
                return Confirm(request);
            default:
                return null;
        }
    }

    private void Open(Request request)
    {
        // A picker replaced while its pick is pending is gone: nothing can confirm it any more,
        // so the walk must not await it forever.
        _pick?.TrySetResult(null);
        _kind = request.Kind;
        _titlePrefix = request.TitlePrefix;
        _labels = request.Labels;
        _values = request.Values;
        _filter = string.Empty;
        _menu = request.Menu;
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

    /// <summary>Esc outside the wizard: the picker is gone and the pick it held is dismissed.</summary>
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

    /// <summary>A wizard navigation verb: the picker is gone and its pick completes with the verb,
    /// never with a row. A picker with no pending pick ignores the verb and stays open.</summary>
    private void Navigate(Request request)
    {
        if (!IsCurrent(request.Expected) || _pick is not { Task.IsCompleted: false } pick)
        {
            return;
        }

        Clear();
        pick.TrySetResult(request.Value);
    }

    private PickerConfirmation? Confirm(Request request)
    {
        if (!IsCurrent(request.Expected) || SelectedValue() is not { } value)
        {
            // Without a current picker or a selected row Enter is a no-op: the picker stays open.
            return null;
        }

        PickerKind kind = _kind;
        TaskCompletionSource<string?>? pick = _pick;
        Clear();
        pick?.TrySetResult(value);
        return new PickerConfirmation(kind, value);
    }

    private bool IsCurrent(SelectListModel? expected) =>
        _picker is not null && (expected is null || ReferenceEquals(_picker, expected));

    private void Clear()
    {
        _picker = null;
        _kind = default;
        _titlePrefix = string.Empty;
        _labels = [];
        _values = [];
        _visibleValues = [];
        _filter = string.Empty;
        _menu = false;
        _pick = null;
        Publish();
    }
}
