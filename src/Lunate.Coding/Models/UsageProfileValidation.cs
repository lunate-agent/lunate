using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The one usage/profile compatibility rule every entry shares: a chat usage refuses a
/// <c>systemone</c> profile, because the System One family is decision-only and no chat client is
/// ever built for it. The interactive wizard, <c>/setup</c>, the <c>lunate setup</c> flag path and
/// startup all call this, so one change to the rule and one wording reach every surface.
/// </summary>
internal static class UsageProfileValidation
{
    /// <summary>The sentence every systemone chat refusal carries, so a caller can recognise it
    /// without depending on a second exception type (`InvalidDataException` is sealed).</summary>
    internal const string RefusalMarker = "System One is decision-only";

    /// <summary>Whether an exception is this shared usage/profile refusal.</summary>
    internal static bool IsRefusal(Exception exception) =>
        exception is InvalidDataException
        && exception.Message.Contains(RefusalMarker, StringComparison.Ordinal);

    /// <summary>Refuses a resolved profile that cannot serve a chat usage.</summary>
    internal static void RequireChatFamily(ResolvedModelProfile profile, string usage) =>
        RequireChatFamily(profile.Profile.Name, profile.Model.Id, profile.Model.ApiFamily, usage);

    /// <summary>
    /// Refuses a profile name and its resolved model family when the usage is a chat usage. The
    /// message names the profile, the model, the family and the exact command that repairs it.
    /// </summary>
    internal static void RequireChatFamily(
        string profileName,
        string modelId,
        string? apiFamily,
        string usage
    )
    {
        if (!string.Equals(apiFamily, ProviderApiFamilies.SystemOne, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidDataException(
            $"Profile '{profileName}' resolves model '{modelId}' of the 'systemone' API family; "
                + $"System One is decision-only and cannot serve the '{usage}' usage. Choose a chat "
                + "model for it, or read decisions from it through 'decisions.profile'; run "
                + "`lunate setup` (or /setup in a session) to change the binding."
        );
    }
}
