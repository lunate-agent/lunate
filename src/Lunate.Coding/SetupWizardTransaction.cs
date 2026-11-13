using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The setup wizard's transactional write: every new body is built in a staging directory beside
/// the store, then the files are replaced in the caller's order — auth first in the wizard's
/// commit, so a failure can never strand a credential — and a failure at any point restores each
/// file already replaced byte for byte. A failed commit therefore leaves auth.json, providers.json
/// and settings.json exactly as they were, and no in-memory state has to be undone.
/// </summary>
internal sealed class SetupWizardTransaction : IDisposable
{
    private readonly string _root;

    private SetupWizardTransaction(string root)
    {
        _root = root;
        Auth = Path.Combine(root, "auth.json");
        Providers = Path.Combine(root, "providers.json");
        Settings = Path.Combine(root, "settings.json");
    }

    /// <summary>The staged body of auth.json.</summary>
    public string Auth { get; }

    /// <summary>The staged body of providers.json.</summary>
    public string Providers { get; }

    /// <summary>The staged body of settings.json.</summary>
    public string Settings { get; }

    /// <summary>
    /// Creates the staging directory beside the auth file — or beside the settings file when the
    /// walk stages no credential — inside the store, never in a shared temp.
    /// </summary>
    public static SetupWizardTransaction Create(string settingsPath, string? authPath)
    {
        string seed = authPath ?? settingsPath;
        string? directory = Path.GetDirectoryName(Path.GetFullPath(seed));
        string parent = string.IsNullOrEmpty(directory) ? "." : directory;
        string staging = Path.Combine(parent, ".setup-" + Path.GetRandomFileName());
        Directory.CreateDirectory(staging);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                staging,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }

        return new SetupWizardTransaction(staging);
    }

    /// <summary>
    /// Claims the stores a commit replaces — the settings file and the provider store — before
    /// anything is seeded or staged (spec: Cross-process settings writes, Cross-process
    /// provider-store writes). The seed then reads what another process has already written, and
    /// the claim is still held when the staged body replaces the target, so two processes merge
    /// instead of the later one winning from a stale read. Settings is claimed before the provider
    /// store so two commits in two processes cannot deadlock on each other.
    /// <para>
    /// Nothing is written before both claims are held: a claim that cannot be taken refuses with
    /// the store's own error and the commit has touched no target. The bounded wait is the store's;
    /// the commit itself holds the claims for one non-interactive pass.
    /// </para>
    /// </summary>
    public static IDisposable ClaimTargets(
        string settingsPath,
        string providerStorePath,
        TimeSpan? claimTimeout = null
    )
    {
        FileLock settings = claimTimeout is { } settingsWait
            ? SettingsStore.AcquireWriteLock(settingsPath, settingsWait)
            : SettingsStore.AcquireWriteLock(settingsPath);
        try
        {
            FileLock providers = claimTimeout is { } providerWait
                ? ProviderStore.AcquireLock(providerStorePath, providerWait)
                : ProviderStore.AcquireLock(providerStorePath);
            return new TargetClaims([settings, providers]);
        }
        catch
        {
            settings.Dispose();
            throw;
        }
    }

    /// <summary>The claims a commit holds, released in the reverse order they were taken.</summary>
    private sealed class TargetClaims(IReadOnlyList<FileLock> claims) : IDisposable
    {
        public void Dispose()
        {
            for (var index = claims.Count - 1; index >= 0; index--)
            {
                claims[index].Dispose();
            }
        }
    }

    /// <summary>Copies an existing file into the stage: every store writer is a read-modify-write.</summary>
    public static void Seed(string source, string staged)
    {
        if (File.Exists(source))
        {
            File.Copy(source, staged, overwrite: true);
        }
    }

    /// <summary>
    /// Replaces each staged body over its target, in order, and restores every file already
    /// replaced when one replace fails. A staged body byte-equal to its target is skipped, so a
    /// commit that changed nothing keeps the file's identity, not just its bytes.
    /// </summary>
    public void Commit(IReadOnlyList<StagedFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        List<Replacement> replaced = [];
        try
        {
            foreach (StagedFile file in files)
            {
                if (!File.Exists(file.Staged) || Unchanged(file.Staged, file.Target))
                {
                    continue;
                }

                Replacement previous = Snapshot(file.Target);
                ReplaceStaged(file.Staged, file.Target);
                replaced.Add(previous);
            }
        }
        catch (Exception failure)
        {
            List<Exception> restoreFailures = [];
            for (var index = replaced.Count - 1; index >= 0; index--)
            {
                try
                {
                    Restore(replaced[index]);
                }
                catch (Exception restoreFailure)
                {
                    restoreFailures.Add(restoreFailure);
                }
            }

            if (restoreFailures.Count > 0)
            {
                throw new IOException(
                    $"setup: the commit failed and {restoreFailures.Count} file(s) could not be restored; "
                        + "check the Lunate store before retrying.",
                    new AggregateException([failure, .. restoreFailures])
                );
            }

            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>One staged body and the target it replaces.</summary>
    internal sealed record StagedFile(string Staged, string Target);

    /// <summary>One file to replace, and the bytes and POSIX mode it had before (null: it did not exist).</summary>
    private sealed record Replacement(string Target, byte[]? Previous, UnixFileMode? Mode);

    private static bool Unchanged(string staged, string target) =>
        File.Exists(target)
        && File.ReadAllBytes(staged).AsSpan().SequenceEqual(File.ReadAllBytes(target));

    private static Replacement Snapshot(string target)
    {
        if (!File.Exists(target))
        {
            return new Replacement(target, Previous: null, Mode: null);
        }

        UnixFileMode? mode = OperatingSystem.IsWindows() ? null : File.GetUnixFileMode(target);
        return new Replacement(target, File.ReadAllBytes(target), mode);
    }

    /// <summary>
    /// Copies the staged body onto the target through a same-directory temp and an atomic replace,
    /// keeping the target's POSIX mode (and the staged file's own when the target is new, which is
    /// how auth.json stays owner-only).
    /// </summary>
    private static void ReplaceStaged(string stagedPath, string targetPath)
    {
        UnixFileMode? mode =
            OperatingSystem.IsWindows() ? null
            : File.Exists(targetPath) ? File.GetUnixFileMode(targetPath)
            : File.GetUnixFileMode(stagedPath);
        ReplaceBytes(targetPath, File.ReadAllBytes(stagedPath), mode);
    }

    /// <summary>Restores one replaced file: the bytes it held before the commit, or its absence.</summary>
    private static void Restore(Replacement previous)
    {
        if (previous.Previous is null)
        {
            File.Delete(previous.Target);
            return;
        }

        ReplaceBytes(previous.Target, previous.Previous, previous.Mode);
    }

    /// <summary>Writes exact bytes beside the target and renames them over it.</summary>
    private static void ReplaceBytes(string targetPath, byte[] bytes, UnixFileMode? mode)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        string targetDirectory = string.IsNullOrEmpty(directory) ? "." : directory;
        Directory.CreateDirectory(targetDirectory);
        string temp = Path.Combine(targetDirectory, Path.GetRandomFileName() + ".tmp");
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (mode is { } fileMode && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temp, fileMode);
            }

            FileReplacement.Replace(temp, targetPath);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
