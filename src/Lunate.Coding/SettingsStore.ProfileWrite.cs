using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding;

public static partial class SettingsStore
{
    /// <summary>
    /// Writes the named profile's reasoning level — or removes the key for
    /// <see cref="ThinkingLevel.Off"/>, because a profile has no <c>off</c> — through
    /// read-modify-write: every other value in the existing JSON is preserved, the result is
    /// validated by the same parser, and the file is replaced atomically with
    /// <c>schemaVersion: 2</c>. On any failure the old file is untouched. Returns the refusal —
    /// naming the profile and the file — when the file carries no such profile; nothing is written
    /// then.
    /// <para>
    /// A profile that already carries a <c>reasoningBudget</c> gets it removed: resolution refuses
    /// a profile with both keys ("cannot be combined with reasoningLevel"), so keeping the budget
    /// would write a profile the next load rejects. The explicit level wins because it is what
    /// this command states; <paramref name="removedBudget"/> reports the removal so the session can
    /// say so instead of dropping the value silently.
    /// </para>
    /// </summary>
    internal static string? WriteProfileReasoningLevel(
        string? profileName,
        ThinkingLevel level,
        out bool removedBudget,
        string? filePath = null
    )
    {
        removedBudget = false;
        string path = filePath ?? DefaultFilePath();
        lock (Gate)
        {
            JsonObject root = ReadRoot(path);
            if (
                profileName is not { Length: > 0 }
                || root["profiles"] is not JsonObject profiles
                || profiles[profileName] is not JsonObject profile
            )
            {
                return $"settings: the chat profile '{(profileName is { Length: > 0 } ? profileName : "(none)")}' "
                    + $"is not configured in {path}";
            }

            if (level == ThinkingLevel.Off)
            {
                profile.Remove("reasoningLevel");
            }
            else
            {
                profile["reasoningLevel"] = level.ToString().ToLowerInvariant();
                removedBudget = profile.Remove("reasoningBudget");
            }
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
            return null;
        }
    }
}
