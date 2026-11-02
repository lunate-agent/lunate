namespace Lunate.Ai;

/// <summary>
/// The wire API on a provider's endpoint. Not a wire protocol per preset but per endpoint: a
/// provider - and every model on it - serves exactly one family unless it declares per-family
/// URLs. Family names are stable wire names in settings, presets and catalogs; every reader
/// refuses an unknown one by name.
/// </summary>
public enum ProviderApiFamily
{
    OpenAiChat,
    OpenAiResponses,
    AnthropicMessages,
    Google,
}

/// <summary>
/// Wire-name parse and format for <see cref="ProviderApiFamily"/>: <c>openai-chat</c>,
/// <c>openai-responses</c>, <c>anthropic-messages</c>, <c>google</c>. Parsing is exact-match so a
/// typo never silently maps to another family; the supported set is named in every refusal.
/// </summary>
public static class ProviderApiFamilies
{
    public const string OpenAiChat = "openai-chat";
    public const string OpenAiResponses = "openai-responses";
    public const string AnthropicMessages = "anthropic-messages";
    public const string Google = "google";

    private static readonly (string Name, ProviderApiFamily Family)[] Known =
    [
        (OpenAiChat, ProviderApiFamily.OpenAiChat),
        (OpenAiResponses, ProviderApiFamily.OpenAiResponses),
        (AnthropicMessages, ProviderApiFamily.AnthropicMessages),
        (Google, ProviderApiFamily.Google),
    ];

    /// <summary>The known families in declaration order; list errors in this order.</summary>
    public static IReadOnlyList<ProviderApiFamily> All { get; } =
    [.. Known.Select(static known => known.Family)];

    /// <summary>The wire name of a family.</summary>
    public static string Name(ProviderApiFamily family) =>
        family switch
        {
            ProviderApiFamily.OpenAiChat => OpenAiChat,
            ProviderApiFamily.OpenAiResponses => OpenAiResponses,
            ProviderApiFamily.AnthropicMessages => AnthropicMessages,
            ProviderApiFamily.Google => Google,
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };

    /// <summary>True when the value is exactly the wire name of a known family.</summary>
    public static bool IsKnown(string? value) => TryParse(value, out _);

    /// <summary>The comma-separated wire names of the known families, for error messages.</summary>
    public static string Supported() => string.Join(", ", Known.Select(static known => known.Name));

    /// <summary>
    /// The comma-separated wire names of the given families, in <see cref="All"/> order and free
    /// of duplicates — the list an ambiguity error shows the person who must choose.
    /// </summary>
    public static string NamesFor(IEnumerable<ProviderApiFamily> families)
    {
        HashSet<ProviderApiFamily> declared = families.ToHashSet();
        return string.Join(", ", All.Where(declared.Contains).Select(Name));
    }

    /// <summary>Parses a wire name; false for null, empty or unknown values.</summary>
    public static bool TryParse(string? value, out ProviderApiFamily family)
    {
        if (value is not null)
        {
            foreach ((string name, ProviderApiFamily known) in Known)
            {
                if (string.Equals(value, name, StringComparison.Ordinal))
                {
                    family = known;
                    return true;
                }
            }
        }

        family = default;
        return false;
    }

    /// <summary>
    /// The refusal for an unknown family name: the value, what was read, and the supported set —
    /// a model-readable error, so prefix it with the key that carried the value.
    /// </summary>
    public static string Unsupported(string? value, string field) =>
        $"Unknown API family '{value ?? ""}' in {field}; supported families: {Supported()}.";
}
