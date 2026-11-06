using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The settings writes the setup wizard's commit makes — one writer each for profiles, usages and
/// the decisions keys it owns (spec: One settings writer). Every write follows the
/// <c>WriteFavorites</c> pattern: read the file, apply the parsed change, validate the result
/// through the same parser, replace the file atomically with its <c>schemaVersion</c> and every
/// untouched section preserved. Nothing is formatted here: the store's own writer is the only JSON
/// emitter for the wizard's keys.
/// </summary>
public static partial class SettingsStore
{
    /// <summary>The usages the wizard may bind; the schema's own list keeps one authority.</summary>
    internal static readonly IReadOnlyList<string> WizardUsages = ["chat", "fast"];

    /// <summary>
    /// Writes one profile — creating it when absent, merging into it when present — where every
    /// other profile and setting is preserved. Only the keys the wizard owns are written
    /// (<c>provider</c>, <c>model</c>, <c>reasoningLevel</c>, <c>apiFamily</c>,
    /// <c>description</c>); a key the wizard has no value for is removed, because the walk owns
    /// it, while every other key in the existing profile object — <c>maxOutput</c>,
    /// <c>temperature</c>, <c>reasoningBudget</c>, <c>contextWindow</c>,
    /// <c>capabilities</c> — survives a re-run (S-29).
    /// </summary>
    internal static void WriteProfile(ModelProfile profile, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string path = filePath ?? DefaultFilePath();
        lock (Gate)
        {
            JsonObject root = ReadRoot(path);
            WriteProfileLocked(root, profile);
            WriteValidated(path, root);
        }
    }

    /// <summary>
    /// Binds a usage to a profile — the <c>chat</c> profile the fast step extends with
    /// <c>fast</c>. Setting a usage without its profile refusing is the parser's own rule; the
    /// wizard binds only names it wrote or the settings file already configures.
    /// </summary>
    public static void WriteUsage(string usage, string profileName, string? filePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usage);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        if (!WizardUsages.Contains(usage, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Usage '{usage}' is not a key the setup wizard binds; bindable usages: {string.Join(", ", WizardUsages)}.",
                nameof(usage)
            );
        }

        string path = filePath ?? DefaultFilePath();
        lock (Gate)
        {
            JsonObject root = ReadRoot(path);
            JsonObject usages = root["usages"] as JsonObject ?? new JsonObject();
            usages[usage] = profileName;
            root["usages"] = usages;
            WriteValidated(path, root);
        }
    }

    /// <summary>
    /// Writes one decisions key of the wizard's set (the profile binding, the consumers' modes,
    /// the routing profile list) — or removes it for a null value, because the accepted defaults
    /// write nothing and a null clears rather than invents. The key names are the schema's; an
    /// unknown one is refused by name before anything is written. A list value writes as a JSON
    /// array (<c>routingProfiles</c>); every other value writes as its JSON literal.
    /// </summary>
    public static void WriteDecisionsKey(string key, JsonNode? value, string? filePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_decisionsKeys.Contains(key, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"'decisions.{key}' is not a key the setup wizard writes; writable keys: {string.Join(", ", _decisionsKeys)}.",
                nameof(key)
            );
        }

        string path = filePath ?? DefaultFilePath();
        lock (Gate)
        {
            JsonObject root = ReadRoot(path);
            JsonObject decisions = root["decisions"] as JsonObject ?? new JsonObject();
            if (value is null)
            {
                decisions.Remove(key);
            }
            else
            {
                decisions[key] = value.DeepClone();
            }

            if (decisions.Count > 0)
            {
                root["decisions"] = decisions;
            }
            else
            {
                root.Remove("decisions");
            }

            WriteValidated(path, root);
        }
    }

    internal static readonly IReadOnlyList<string> _decisionsKeys =
    [
        "profile",
        "approvals",
        "routing",
        "tool",
        "questions",
        "routingProfiles",
    ];

    private static void WriteProfileLocked(JsonObject root, ModelProfile profile)
    {
        JsonObject profiles = root["profiles"] as JsonObject ?? new JsonObject();
        // Merge into the existing object: a re-run replaces the values the wizard owns and leaves
        // the hand-set ones a walk never collects in place.
        JsonObject existing = profiles[profile.Name] as JsonObject ?? new JsonObject();
        existing["provider"] = profile.Provider;
        existing["model"] = profile.Model;

        // A level and a budget cannot be combined (ModelProfileResolver); the level wins because
        // the level is what this write states, and the budget is dropped deterministically.
        SetReasoningLevel(existing, profile.ReasoningLevel);

        SetOptionalString(existing, "apiFamily", profile.ApiFamily);
        SetOptionalString(existing, "description", profile.Description);
        profiles[profile.Name] = existing;
        root["profiles"] = profiles;
    }

    /// <summary>
    /// Sets the profile's <c>reasoningLevel</c> — or removes the key when the walk carries no
    /// level — and removes a <c>reasoningBudget</c> the level would make the profile refuse:
    /// resolution treats the two as mutually exclusive, so an inherited budget would otherwise
    /// turn a valid write into a profile the next load rejects (S-28).
    /// </summary>
    private static void SetReasoningLevel(JsonObject profile, string? level)
    {
        if (level is { } value)
        {
            profile["reasoningLevel"] = value;
            profile.Remove("reasoningBudget");
        }
        else
        {
            profile.Remove("reasoningLevel");
        }
    }

    /// <summary>Sets a wizard-owned string key, or removes it when the walk carries no value.</summary>
    private static void SetOptionalString(JsonObject profile, string key, string? value)
    {
        if (value is { Length: > 0 } declared)
        {
            profile[key] = declared;
        }
        else
        {
            profile.Remove(key);
        }
    }

    /// <summary>Serializes, parser-validates and atomically replaces the file; shared by the wizard writers.</summary>
    private static void WriteValidated(string path, JsonObject root)
    {
        root["schemaVersion"] = SupportedSchemaVersion;
        string json = root.ToJsonString(WriteOptions) + "\n";
        var problems = new List<string>();
        _ = ParseJson(json, problems);
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid settings in '{path}': {string.Join("; ", problems)}. No settings were written."
            );
        }

        WriteAtomically(path, json);
    }
}
