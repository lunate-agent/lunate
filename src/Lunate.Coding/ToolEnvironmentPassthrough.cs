namespace Lunate.Coding;

/// <summary>
/// The tool-process environment's explicit pass-through list: the exact variable names a user
/// allows into every tool child, applied after the inherited policy's credential strip. The list
/// is user-scope settings (<c>toolEnvironment.passthrough</c>), resolved once at startup; it is
/// process-wide for the same reason <see cref="CredentialVariables"/> is — the tool child's
/// environment is built in one shared spawn path, and <c>ChildProcess.BuildEnvironment</c> reads
/// the list there, where the policy is built, never at a spawn site.
/// </summary>
internal static class ToolEnvironmentPassthrough
{
    /// <summary>
    /// The environment-name comparison the builder uses: Windows names are case-insensitive, so a
    /// listed spelling that differs only in case from a credential name is still that name.
    /// </summary>
    private static readonly StringComparer Comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static readonly Lock Gate = new();

    private static IReadOnlyList<string> _names = [];

    /// <summary>The configured names, exact and in the order the user wrote them.</summary>
    internal static IReadOnlyList<string> Names
    {
        get
        {
            lock (Gate)
            {
                return _names;
            }
        }
    }

    /// <summary>
    /// Installs the configured names (trimmed, blanks dropped, duplicates removed) and returns what
    /// was admitted plus the names that are also Lunate credential names — the names a startup
    /// warning must carry. The named variables are still passed through: an explicit name is an
    /// explicit user act. The frontends render the admitted names from this result rather than
    /// reading <see cref="Names"/>, so only the policy-build site reads the process-wide list.
    /// </summary>
    internal static Admission Configure(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        IReadOnlyList<string> configured =
        [
            .. names
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal),
        ];

        lock (Gate)
        {
            _names = configured;
            IReadOnlyList<string> credentials = CredentialVariables.All;
            return new Admission(
                configured,
                [.. configured.Where(name => credentials.Contains(name, Comparer))]
            );
        }
    }

    /// <summary>The installed names and the credential-name collisions among them.</summary>
    internal sealed record Admission(IReadOnlyList<string> Names, IReadOnlyList<string> Collisions);

    /// <summary>
    /// The one startup line naming the variables admitted into tool processes, so the user can see
    /// what they allowed whatever the collision state. Empty when nothing is admitted, which keeps
    /// a default install quiet.
    /// </summary>
    internal static string AdmissionNotice(IReadOnlyList<string> names) =>
        names.Count == 0
            ? string.Empty
            : $"toolEnvironment.passthrough admits {string.Join(", ", names)} into tool processes.";

    /// <summary>
    /// The one startup line naming a pass-through entry that is also a credential: the user asked
    /// for it by name, so it is allowed, and the warning is the only signal.
    /// </summary>
    internal static string CollisionWarning(IReadOnlyList<string> collisions) =>
        $"toolEnvironment.passthrough admits {string.Join(", ", collisions)} into tool "
        + "processes; Lunate also resolves "
        + (collisions.Count == 1 ? "that name" : "those names")
        + " as a provider credential.";

    /// <summary>Clears the configured names; the process-wide list is what tests must not inherit.</summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _names = [];
        }
    }
}
