namespace Lunate.Ai;

/// <summary>
/// A provider preset: the base URL and key variable that make a provider usable without a
/// hand-written model entry. <see cref="KeyVariableAlias"/> is an optional second environment
/// variable accepted for the same provider (for example <c>GEMINI_API_KEY</c> for Google).
/// A preset may declare per-family URLs (<see cref="BaseUrls"/>, families may share a URL), a
/// discovery <see cref="ModelsUrl"/>, and <see cref="Headers"/> — headers are declared data and
/// the headers pipeline sends them; until that lands they are not sent.
/// </summary>
public sealed record ProviderPreset(
    string Id,
    string Name,
    ProviderAdapter Adapter,
    Uri BaseUrl,
    string KeyVariable,
    string? KeyVariableAlias = null,
    IReadOnlyDictionary<ProviderApiFamily, Uri>? BaseUrls = null,
    Uri? ModelsUrl = null,
    IReadOnlyDictionary<string, string>? Headers = null
)
{
    /// <summary>
    /// The family <see cref="BaseUrl"/> names: <c>anthropic-messages</c> for the Anthropic
    /// adapter, <c>openai-chat</c> for everything else. The built-in catalog is annotated with
    /// it for presets that declare more than one family, so no model is ambiguous.
    /// </summary>
    public ProviderApiFamily DefaultFamily =>
        Adapter == ProviderAdapter.Anthropic
            ? ProviderApiFamily.AnthropicMessages
            : ProviderApiFamily.OpenAiChat;

    /// <summary>
    /// The families this preset declares: the keys of <see cref="BaseUrls"/> (in stable
    /// declaration order) when present, otherwise just <see cref="DefaultFamily"/>.
    /// </summary>
    public IReadOnlyList<ProviderApiFamily> Families =>
        BaseUrls is { Count: > 0 }
            ? [.. ProviderApiFamilies.All.Where(BaseUrls.ContainsKey)]
            : [DefaultFamily];

    /// <summary>
    /// The URL a family routes to, or null when the preset does not declare it. Without
    /// <see cref="BaseUrls"/> only <see cref="DefaultFamily"/> resolves, to <see cref="BaseUrl"/>.
    /// </summary>
    public Uri? UrlFor(ProviderApiFamily family) =>
        BaseUrls is { Count: > 0 } ? BaseUrls.GetValueOrDefault(family)
        : family == DefaultFamily ? BaseUrl
        : null;
}
