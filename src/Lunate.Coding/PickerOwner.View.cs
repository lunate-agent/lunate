using System.Globalization;
using Lunate.Tui;

namespace Lunate.Coding;

/// <summary>
/// The picker as other threads read it: one immutable snapshot per applied transition, so a
/// reader never sees a half-open picker. <see cref="FiltersOn"/> decides whether a printable
/// character filters the rows and whether the wizard's bare-letter verbs are available.
/// </summary>
internal sealed record PickerSnapshot(
    SelectListModel? Picker,
    PickerKind Kind,
    string Filter,
    bool FiltersOn,
    bool Menu
)
{
    public bool IsOpen => Picker is not null;

    /// <summary>Whether the open picker belongs to the wizard walk, whose pick it completes.</summary>
    public bool IsWizard =>
        Kind is PickerKind.WizardProvider or PickerKind.WizardChoice or PickerKind.WizardModel;
}

/// <summary>What a confirmed picker row means, answered only after the owner has dismissed the picker.</summary>
internal sealed record PickerConfirmation(PickerKind Kind, string Value);

// The owner's state -> view derivation: the rows, the filter, the title and the snapshot other
// threads read. Only the owner's consumer loop calls these.

internal sealed partial class PickerOwner
{
    /// <summary>
    /// Rebuilds the visible rows from the full list and the filter, keeping
    /// <paramref name="keepValue"/> selected when it still matches and falling back to
    /// <paramref name="fallback"/> otherwise; the session picker never filters.
    /// </summary>
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
        Volatile.Write(
            ref _snapshot,
            new PickerSnapshot(picker, _kind, _filter, FiltersOn(), _menu)
        );
        _post(picker);
    }

    /// <summary>Whether the open picker filters as text is typed: the model pickers always (their
    /// listing can be long and the search is their documented affordance), the other wizard pickers
    /// only when the list exceeds <see cref="FilterThreshold"/> rows — a short list keeps bare
    /// <c>b</c> and <c>f</c> free for the wizard's back and forward verbs (spec: The walk is
    /// navigable).</summary>
    private bool FiltersOn() =>
        _kind is PickerKind.Model or PickerKind.WizardModel
        || (
            _kind is PickerKind.WizardProvider or PickerKind.WizardChoice
            && _labels.Count > FilterThreshold
        );

    private bool Matches(string label) =>
        !FiltersOn()
        || _filter.Length == 0
        || label.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>The row label; the active model keeps its <c>* </c> marker.</summary>
    private string Label(int index) =>
        _kind is PickerKind.Model
        && string.Equals(_values[index], _currentModelId(), StringComparison.Ordinal)
            ? "* " + _labels[index]
            : _labels[index];

    /// <summary>
    /// The title. The session picker keeps its plain title; the model picker appends the
    /// invariant position and visible count and, while filtering, the filter text.
    /// </summary>
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
