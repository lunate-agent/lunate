using System.Text.Json;

namespace Lunate.Coding;

/// <summary>
/// The <c>toolEnvironment</c> settings section: the exact environment variable names a user admits
/// into tool processes after the inherited policy's credential strip. Values live at the top level
/// of the same file; this section only names variables to pass through, never their values.
/// </summary>
public static partial class SettingsStore
{
    /// <summary>
    /// Reads the <c>toolEnvironment</c> section: only <c>passthrough</c> is known, an array of exact
    /// variable names. A missing key keeps the documented default (empty); every bad entry is
    /// collected. A pattern is not rejected — an exact name such as <c>OPENAI_*</c> simply matches
    /// nothing, because no name is ever treated as a pattern.
    /// </summary>
    private static ToolEnvironmentSettings ReadToolEnvironment(
        JsonElement value,
        List<string> problems
    )
    {
        var settings = new ToolEnvironmentSettings();
        if (value.ValueKind != JsonValueKind.Object)
        {
            problems.Add("'toolEnvironment' must be an object");
            return settings;
        }

        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Name != "passthrough")
            {
                problems.Add($"unknown key 'toolEnvironment.{property.Name}'");
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(
                    "'toolEnvironment.passthrough' must be an array of environment variable names"
                );
                continue;
            }

            List<string> names = [];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement item in property.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    problems.Add(
                        "'toolEnvironment.passthrough' must contain only environment variable names"
                    );
                    continue;
                }

                string name = item.GetString()!.Trim();
                if (name.Length == 0 || name.Contains('=', StringComparison.Ordinal))
                {
                    // A name, not a NAME=value pair, and never an empty entry.
                    problems.Add(
                        "'toolEnvironment.passthrough' must contain only environment variable names"
                    );
                    continue;
                }

                if (!seen.Add(name))
                {
                    problems.Add(
                        $"'toolEnvironment.passthrough' must be unique; '{name}' appears more than once"
                    );
                    continue;
                }

                names.Add(name);
            }

            settings = settings with { Passthrough = names };
        }

        return settings;
    }
}
