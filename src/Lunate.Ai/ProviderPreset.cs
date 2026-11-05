namespace Lunate.Ai;

/// <summary>
/// A provider preset: the base URL and key variable that make a provider usable without a
/// hand-written model entry. <see cref="KeyVariableAlias"/> is an optional second environment
/// variable accepted for the same provider (for example <c>GEMINI_API_KEY</c> for Google).
/// A preset may declare per-family URLs (<see cref="BaseUrls"/>, families may share a URL), a
/// discovery <see cref="ModelsUrl"/>, <see cref="Headers"/> (values literal, template or
/// credential reference), per-family <see cref="CredentialPlacements"/> and per-family headers
/// (<see cref="FamilyHeaders"/>, sent only on that family's wire).
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
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<ProviderApiFamily, CredentialPlacement>? CredentialPlacements = null,
    IReadOnlyDictionary<ProviderApiFamily, IReadOnlyDictionary<string, string>>? FamilyHeaders =
        null
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
    /// The families this preset declares: the keys of <see cref="BaseUrls"/>, plus
    /// <see cref="DefaultFamily"/> when only the <c>baseUrl</c> shorthand declares it (families
    /// may share a URL; a per-family URL never removes the shorthand's family), in stable
    /// declaration order. Otherwise just <see cref="DefaultFamily"/>.
    /// </summary>
    public IReadOnlyList<ProviderApiFamily> Families =>
        BaseUrls is { Count: > 0 } bUrls
            ?
            [
                .. ProviderApiFamilies.All.Where(family =>
                    bUrls.ContainsKey(family) || family == DefaultFamily
                ),
            ]
            : [DefaultFamily];

    /// <summary>
    /// The families a chat model may resolve to: <see cref="Families"/> without the decision-only
    /// <see cref="ProviderApiFamily.SystemOne"/>. A provider that also offers System One serves
    /// exactly one chat family that way, so an unannotated chat model still resolves instead of
    /// failing as ambiguous.
    /// </summary>
    public IReadOnlyList<ProviderApiFamily> ChatFamilies =>
        [.. Families.Where(static family => family != ProviderApiFamily.SystemOne)];

    /// <summary>
    /// The URL a family routes to, or null when the preset does not declare it. Without
    /// <see cref="BaseUrls"/> only <see cref="DefaultFamily"/> resolves, to <see cref="BaseUrl"/>.
    /// </summary>
    public Uri? UrlFor(ProviderApiFamily family) =>
        BaseUrls is { Count: > 0 } ? BaseUrls.GetValueOrDefault(family)
        : family == DefaultFamily ? BaseUrl
        : null;

    /// <summary>
    /// The credential placement a family's wire uses: the family's own declaration, or the
    /// default <see cref="CredentialPlacement.Bearer"/> when the preset declares none. The value
    /// stays in the credential pipeline; a placement is not a secret.
    /// </summary>
    public CredentialPlacement PlacementFor(ProviderApiFamily family) =>
        CredentialPlacements?.GetValueOrDefault(family) ?? CredentialPlacement.Bearer;

    /// <summary>
    /// The headers a family's wire sends on its own: image-only headers (System One's
    /// <c>TypeSafe-Danger-Zone-Images</c>) live here, so a family's headers never reach another
    /// family's wire.
    /// </summary>
    public IReadOnlyDictionary<string, string>? HeadersFor(ProviderApiFamily family) =>
        FamilyHeaders?.GetValueOrDefault(family);
}
