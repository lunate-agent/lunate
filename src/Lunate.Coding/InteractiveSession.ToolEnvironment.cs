namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    /// <summary>
    /// Installs the tool-process pass-through list from the resolved settings: one line names the
    /// admitted variables, and one warning follows when a listed name is also a Lunate credential
    /// name. Called wherever settings are resolved or re-resolved, before any tool child can run;
    /// the list itself is read when the tool child's policy is built
    /// (<c>ChildProcess.BuildEnvironment</c>), never at a spawn site.
    /// </summary>
    private void ApplyToolEnvironment()
    {
        ToolEnvironmentPassthrough.Admission admission = ToolEnvironmentPassthrough.Configure(
            _settings.ToolEnvironment.Passthrough
        );
        if (admission.Names.Count > 0)
        {
            Notify(ToolEnvironmentPassthrough.AdmissionNotice(admission.Names), NoticeLevel.Info);
        }

        // The mirror of the admission line: a user whose NPM_TOKEN vanished must be able to see why.
        // Names only, never values, and it says where the way back in is. Read from this process's
        // environment, which is what a tool child inherits from, and computed rather than stored so
        // it describes the machine the session is actually running on.
        IReadOnlyList<string> stripped = ToolEnvironmentPassthrough.StrippedNames(
            [.. Environment.GetEnvironmentVariables().Keys.Cast<string>()]
        );
        if (stripped.Count > 0)
        {
            Notify(ToolEnvironmentPassthrough.StrippedNotice(stripped), NoticeLevel.Info);
        }

        if (admission.Collisions.Count > 0)
        {
            Notify(
                ToolEnvironmentPassthrough.CollisionWarning(admission.Collisions),
                NoticeLevel.Warning
            );
        }
    }
}
