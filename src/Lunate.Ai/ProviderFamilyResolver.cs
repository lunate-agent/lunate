namespace Lunate.Ai;

/// <summary>
/// The family a model call routes through, shared by the profile resolver and the client
/// factory. Precedence: an already-merged declared family (profile override, then discovery
/// metadata, then the catalog annotation) wins; with nothing declared, the provider answers —
/// and only when it serves exactly one chat family. Two chat families with no answer is an error
/// naming the provider, the model and the families to choose from. The decision-only
/// <c>systemone</c> family never counts as a chat answer, even when the caller hands in the
/// provider's full family list.
/// </summary>
internal static class ProviderFamilyResolver
{
    public static ProviderApiFamily Resolve(
        string? declaredFamily,
        IReadOnlyList<ProviderApiFamily> offered,
        string providerLabel,
        string modelId
    )
    {
        if (declaredFamily is { } declared)
        {
            if (!ProviderApiFamilies.TryParse(declared, out ProviderApiFamily family))
            {
                throw new InvalidOperationException(
                    $"Model '{modelId}' on provider '{providerLabel}' declares unknown API family "
                        + $"'{declared}'; supported families: {ProviderApiFamilies.Supported()}."
                );
            }

            return family;
        }

        IReadOnlyList<ProviderApiFamily> chatFamilies =
        [
            .. offered.Where(static family => family != ProviderApiFamily.SystemOne),
        ];
        if (chatFamilies is [ProviderApiFamily only])
        {
            return only;
        }

        throw new InvalidOperationException(
            $"Model '{modelId}' on provider '{providerLabel}' serves more than one chat API family "
                + $"({ProviderApiFamilies.NamesFor(chatFamilies)}); set 'apiFamily' in the model's profile "
                + "or models.json entry, or declare one family per provider entry."
        );
    }
}
