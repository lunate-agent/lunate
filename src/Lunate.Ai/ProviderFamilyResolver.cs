namespace Lunate.Ai;

/// <summary>
/// The family a model call routes through, shared by the profile resolver and the client
/// factory. Precedence: an already-merged declared family (profile override, then discovery
/// metadata, then the catalog annotation) wins; with nothing declared, the provider answers —
/// and only when it serves exactly one family. Two families with no answer is an error naming
/// the provider, the model and the families to choose from.
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

        if (offered is [ProviderApiFamily only])
        {
            return only;
        }

        throw new InvalidOperationException(
            $"Model '{modelId}' on provider '{providerLabel}' serves more than one API family "
                + $"({ProviderApiFamilies.NamesFor(offered)}); set 'apiFamily' on the model's profile "
                + "or declare one family per provider entry."
        );
    }
}
