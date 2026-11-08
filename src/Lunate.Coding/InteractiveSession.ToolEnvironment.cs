namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    /// <summary>
    /// Installs the tool-process pass-through list from the resolved settings, warning once when a
    /// listed name is also a Lunate credential name. Called wherever settings are resolved or
    /// re-resolved, before any tool child can run; the list itself is read when the tool child's
    /// policy is built (<c>ChildProcess.BuildEnvironment</c>), never at a spawn site.
    /// </summary>
    private void ApplyToolEnvironment()
    {
        if (
            ToolEnvironmentPassthrough.Configure(_settings.ToolEnvironment.Passthrough) is
            { Count: > 0 } collisions
        )
        {
            Notify(ToolEnvironmentPassthrough.CollisionWarning(collisions), NoticeLevel.Warning);
        }
    }
}
