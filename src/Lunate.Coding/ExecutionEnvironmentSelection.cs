using System.Globalization;

namespace Lunate.Coding;

/// <summary>
/// The container environment a session may run its command tools in, parsed from
/// <c>--env</c> or the <c>environment</c> setting. The engine name is normalised; the image is kept
/// verbatim because registries and digests may contain colons and punctuation of their own.
/// </summary>
internal sealed record ExecutionEnvironmentSelection(string Engine, string Image)
{
    /// <summary>The accepted shape, used in every refusal.</summary>
    internal const string Accepted = "docker:<image> or podman:<image>";

    /// <summary>The interactive/print flag that selects an environment for one run.</summary>
    internal const string Flag = "--env";

    /// <summary>The settings key with the same shape.</summary>
    internal const string SettingsKey = "environment";

    /// <summary>The form shown to the user; the engine is already normalised.</summary>
    internal string Display => string.Create(CultureInfo.InvariantCulture, $"{Engine}:{Image}");

    /// <summary>
    /// Parses <paramref name="value"/>. Null or whitespace means "no environment" and is not an
    /// error. A malformed value sets <paramref name="error"/> and never falls back to the host.
    /// </summary>
    internal static bool TryParse(
        string? value,
        out ExecutionEnvironmentSelection? selection,
        out string? error
    )
    {
        selection = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string text = value.Trim();
        int separator = text.IndexOf(':');
        if (separator <= 0 || separator == text.Length - 1)
        {
            error = $"{Flag}/{SettingsKey} must be '{Accepted}', but was '{value}'";
            return false;
        }

        string engine = text[..separator];
        string image = text[(separator + 1)..];
        if (
            engine is not ("docker" or "podman")
            || image.Length == 0
            || image.Any(char.IsWhiteSpace)
            || image.Any(char.IsControl)
        )
        {
            error = $"{Flag}/{SettingsKey} must be '{Accepted}', but was '{value}'";
            return false;
        }

        selection = new ExecutionEnvironmentSelection(engine, image);
        return true;
    }

    /// <summary>Parses a value the store already validated; the caller supplies the fallback.</summary>
    internal static ExecutionEnvironmentSelection? FromValidated(string? value) =>
        TryParse(value, out ExecutionEnvironmentSelection? selection, out _) ? selection : null;
}
