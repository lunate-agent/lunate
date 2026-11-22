using System.Text;
using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// Reads and writes <c>~/.lunate/auth.json</c> (schema 1): named API keys for per-model
/// credential references. <see cref="Save"/> takes the store's cross-process claim beside the file
/// and re-reads the document under it, applies the keys this store set and removed since its load,
/// and replaces the file atomically with owner-only permissions on POSIX — so a key another process
/// saved is merged, not dropped. The provider preset's environment variable (and its alias)
/// overrides the corresponding provider entry when it is non-blank; custom named keys have no
/// environment override. Errors name keys, never values. The path and the environment lookup are
/// injectable.
/// </summary>
public sealed class AuthStore
{
    /// <summary>The only supported schema version.</summary>
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // auth.json is Lunate-owned: LF on every platform, like settings.json and providers.json.
        NewLine = "\n",
    };

    /// <summary>How long a save waits for another process's claim before it refuses.</summary>
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Serializes this process's saves. The claim refuses a second handle from this process too, so
    /// two threads saving at once would otherwise wait one another out to the timeout.
    /// </summary>
    private static readonly Lock Gate = new();

    private readonly string _filePath;
    private readonly Func<string, string?> _environment;

    /// <summary>The document as this store last read or wrote it; replaced, never mutated in place, so a reader sees one reading.</summary>
    private Dictionary<string, string> _keys;

    /// <summary>
    /// The keys this store changed since it loaded or last saved, and their new value; a null value
    /// is a removal. A save applies these to the document it re-reads under the claim, so it writes
    /// only what this store meant to change.
    /// </summary>
    private readonly Dictionary<string, string?> _changes = new(StringComparer.Ordinal);

    private AuthStore(
        string filePath,
        Func<string, string?> environment,
        Dictionary<string, string> keys
    )
    {
        _filePath = filePath;
        _environment = environment;
        _keys = keys;
    }

    /// <summary>The stored keys, without environment overrides applied.</summary>
    public IReadOnlyDictionary<string, string> Keys => _keys;

    /// <summary>The auth file this store was loaded from; a staged store commits only after every sibling file is in place.</summary>
    internal string FilePath => _filePath;

    /// <summary>Loads the store; a missing file yields an empty store, a malformed file one error listing every problem.</summary>
    public static AuthStore Load(string? filePath = null, Func<string, string?>? environment = null)
    {
        string path = filePath ?? DefaultFilePath();
        var env = environment ?? Environment.GetEnvironmentVariable;
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return new AuthStore(path, env, keys);
        }

        var problems = new List<string>();
        Parse(path, keys, problems);
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid auth file '{path}': {string.Join("; ", problems)}. No keys were loaded."
            );
        }

        var store = new AuthStore(path, env, keys);
        if (keys.Count > 0)
        {
            AgentSettings settings = SettingsStore.Resolve(SettingsFilePath(filePath), _ => null);
            _ = ProviderStore.Load(ProviderStoreFilePath(filePath), store, settings.Providers);
        }

        return store;
    }

    /// <summary>Returns the named key with provider environment overrides applied, or null when it is missing or blank.</summary>
    public string? TryGet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string? fromEnvironment = EnvironmentOverride(name);
        string? value = string.IsNullOrWhiteSpace(fromEnvironment)
            ? _keys.GetValueOrDefault(name)
            : fromEnvironment;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Returns the named key or throws an error that names the key but never a secret value.</summary>
    public string Require(string name) =>
        TryGet(name)
        ?? throw new InvalidOperationException(
            $"No API key named '{name}' is available in '{_filePath}'. Add it there or set the matching environment variable."
        );

    /// <summary>Sets a named key in memory; call <see cref="Save"/> to persist it.</summary>
    public void Set(string name, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _keys[name] = secret;
        _changes[name] = secret;
    }

    /// <summary>Removes a named key in memory; returns whether it was present. Call <see cref="Save"/> to persist it.</summary>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_keys.Remove(name))
        {
            return false;
        }

        _changes[name] = null;
        return true;
    }

    /// <summary>
    /// Merges this store's changes into the document the file holds and replaces it atomically,
    /// all under the store's cross-process claim: another process's save that lands while this one
    /// waits is kept, and a failed write leaves the old file intact. Nothing is written when this
    /// store changed no key. The replacement is created owner-only on POSIX before the move, so a
    /// secret is never briefly world-readable.
    /// </summary>
    public void Save()
    {
        lock (Gate)
        {
            if (_changes.Count == 0)
            {
                return;
            }

            using FileLock lease = AcquireLock(_filePath);

            // Re-read under the claim: the file, not this store's load, is the base a save merges
            // into, so a key another process wrote in between survives.
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            var problems = new List<string>();
            if (File.Exists(_filePath))
            {
                Parse(_filePath, merged, problems);
                if (problems.Count > 0)
                {
                    throw new InvalidDataException(
                        $"Invalid auth file '{_filePath}': {string.Join("; ", problems)}. "
                            + "No keys were written."
                    );
                }
            }

            foreach ((string name, string? secret) in _changes)
            {
                if (secret is null)
                {
                    merged.Remove(name);
                }
                else
                {
                    merged[name] = secret;
                }
            }

            Write(_filePath, merged);

            // The file is now this store's document: adopt the merged reading, so a later change
            // is tracked against what is actually on disk.
            _keys = merged;
            _changes.Clear();
        }
    }

    private static void Write(string filePath, Dictionary<string, string> keys)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string targetDirectory = string.IsNullOrEmpty(directory) ? "." : directory;
        string temp = Path.Combine(targetDirectory, Path.GetRandomFileName() + ".tmp");
        var sorted = new SortedDictionary<string, string>(keys, StringComparer.Ordinal);
        string json = JsonSerializer.Serialize(
            new AuthFile(SupportedSchemaVersion, sorted),
            SerializerOptions
        );

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temp, options))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
            }

            File.Move(temp, filePath, overwrite: true);

            // Best-effort on Windows: the ACL model has no direct rw------- equivalent here.
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
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

    private static void Parse(string path, Dictionary<string, string> keys, List<string> problems)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            problems.Add($"the file is not valid JSON ({exception.Message})");
            return;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                problems.Add("the root must be a JSON object");
                return;
            }

            bool schemaVersionSeen = false;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "schemaVersion":
                        schemaVersionSeen = true;
                        if (
                            property.Value.ValueKind != JsonValueKind.Number
                            || !property.Value.TryGetInt32(out int version)
                            || version != SupportedSchemaVersion
                        )
                        {
                            problems.Add($"schemaVersion must be {SupportedSchemaVersion}");
                        }

                        break;
                    case "keys":
                        ReadKeys(property.Value, keys, problems);
                        break;
                    default:
                        problems.Add($"unknown key '{property.Name}'");
                        break;
                }
            }

            if (!schemaVersionSeen)
            {
                problems.Add($"schemaVersion is missing; expected {SupportedSchemaVersion}");
            }
        }
    }

    private static void ReadKeys(
        JsonElement element,
        Dictionary<string, string> keys,
        List<string> problems
    )
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            problems.Add("'keys' must be an object mapping names to secrets");
            return;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (
                property.Value.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(property.Value.GetString())
            )
            {
                problems.Add($"key '{property.Name}' must be a non-empty string");
                continue;
            }

            keys[property.Name] = property.Value.GetString()!;
        }
    }

    private string? EnvironmentOverride(string name)
    {
        if (ProviderPresets.Find(name) is not { } preset)
        {
            return null;
        }

        string? value = _environment(preset.KeyVariable);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return preset.KeyVariableAlias is { } alias ? _environment(alias) : null;
    }

    private static string DefaultFilePath() =>
        Path.Combine(Lunate.Ai.LunateHome.Store, "auth.json");

    /// <summary>
    /// The cross-process claim beside an auth file: <c>auth.lock</c> for <c>auth.json</c>. It
    /// serializes one save against another process's read-modify-write; a read does not take it.
    /// </summary>
    internal static string WriteLockPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Path.ChangeExtension(filePath, ".lock");
    }

    internal static FileLock AcquireLock(string filePath) => AcquireLock(filePath, LockTimeout);

    internal static FileLock AcquireLock(string filePath, TimeSpan timeout)
    {
        string lockPath = WriteLockPath(filePath);
        try
        {
            return FileLock.Acquire(lockPath, timeout);
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException(
                $"the auth file '{filePath}' is being written by another Lunate process (lock "
                    + $"'{lockPath}'); nothing was changed. Try again.",
                exception
            );
        }
    }

    internal static string SettingsFilePath(string? authFilePath = null) =>
        SiblingFilePath(authFilePath, "settings.json");

    internal static string ProviderStoreFilePath(string? authFilePath = null) =>
        SiblingFilePath(authFilePath, "providers.json");

    private static string SiblingFilePath(string? authFilePath, string name)
    {
        string authPath = authFilePath ?? DefaultFilePath();
        string directory = Path.GetDirectoryName(Path.GetFullPath(authPath))!;
        return Path.Combine(directory, name);
    }

    private sealed record AuthFile(int SchemaVersion, SortedDictionary<string, string> Keys);
}
