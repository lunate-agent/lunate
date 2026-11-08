using System.Text.Json;

namespace Lunate.Coding;

public static partial class SettingsStore
{
    /// <summary>
    /// Reads the file-only execution environment. A missing key keeps host execution; a malformed
    /// value is one problem and never falls back to the host.
    /// </summary>
    private static string? ReadEnvironment(JsonElement value, List<string> problems)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            problems.Add(
                $"'environment' must be '{ExecutionEnvironmentSelection.Accepted}', but was not a string"
            );
            return null;
        }

        string? text = value.GetString();
        if (
            !ExecutionEnvironmentSelection.TryParse(
                text,
                out ExecutionEnvironmentSelection? selection,
                out string? error
            )
        )
        {
            problems.Add(error!);
            return null;
        }

        return selection?.Display;
    }
}
