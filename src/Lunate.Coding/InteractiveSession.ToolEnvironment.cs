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

        if (admission.Collisions.Count > 0)
        {
            Notify(
                ToolEnvironmentPassthrough.CollisionWarning(admission.Collisions),
                NoticeLevel.Warning
            );
        }
    }
}
