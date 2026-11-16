using Lunate.Tui;

namespace Lunate.Coding;

/// <summary>
/// The user keymap file at startup: the session's own home (<see cref="LunateHome"/>-resolved, or
/// the profile a caller holds), overridden by <c>$LUNATE_KEYMAP</c> and by <c>--keymap</c>. The
/// file is the catalogue's overlay — each binding replaces its keys and nothing else. A file that
/// cannot be applied refuses the load with a warning line and the shipped keys stand; two
/// resolved keys arising from user-vs-catalogue conflicts print one line each.
/// <seealso cref="InteractiveSession.ApplyTuiPresentation"/>
/// </summary>
internal sealed partial class InteractiveSession
{
    /// <summary>
    /// Applies the resolved user keymap file once, at composition time, after the persisted
    /// <c>tui.keys</c> remaps: the file is the overlay over everything the catalogue and the
    /// persisted settings have themselves already made — the file is the strongest and most
    /// explicit choice, so its entries win on the same binding id.
    /// </summary>
    private void ApplyUserKeymap()
    {
        string home = Lunate.Ai.LunateHome.In(_options.HomeDirectory);
        string path = KeymapFile.ResolvePath(
            _options.Launch.Keymap,
            home,
            _options.Environment ?? Environment.GetEnvironmentVariable
        );
        KeymapOutcome outcome = KeymapFile.Apply(path);
        if (outcome.Error is not null)
        {
            Notify(outcome.Error, NoticeLevel.Warning);
            return;
        }

        foreach (string line in outcome.ConflictLines)
        {
            Notify(line, NoticeLevel.Info);
        }
    }
}
