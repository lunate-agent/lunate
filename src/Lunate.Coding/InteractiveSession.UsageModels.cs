using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    private IReadOnlyDictionary<string, UsageModelClient>? ResolveUsageModels(
        ModelInfo fallbackChat
    )
    {
        if (_extensionModelId is not null)
        {
            return null;
        }

        // The host composition asks this before a model resolves (a null fallback), and the setup
        // wizard's session carries a placeholder id until its commit: neither is a selection, so
        // no usage client is built for either (spec: The wizard trigger).
        ResolvedModelProfile? chat = ChatModelResolver.ResolveChatProfile(
            _settings,
            _providerEntries,
            _catalog
        );
        if (chat is null)
        {
            string? fallback = fallbackChat?.Id;
            if (
                string.IsNullOrWhiteSpace(fallback)
                || string.Equals(fallback, NotConfiguredModelId, StringComparison.Ordinal)
            )
            {
                return null;
            }

            chat = ModelSelectionResolver.ResolveChatModel(
                fallback,
                _settings,
                _providerEntries,
                _catalog
            );
        }
        DecisionUsageModels.RequireChatFamily(chat, usage: "chat");
        var clientsByProfile = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal);
        var usages = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
        {
            ["chat"] = GetClient(chat),
        };

        foreach ((string usage, string profileName) in _settings.Usages)
        {
            if (usage == "chat")
            {
                continue;
            }

            try
            {
                ResolvedModelProfile profile = ModelSelectionResolver.ResolveChatModel(
                    profileName,
                    _settings,
                    _providerEntries,
                    _catalog
                );
                if (usage != "decision")
                {
                    DecisionUsageModels.RequireChatFamily(profile, usage: usage);
                }

                usages[usage] = GetClient(profile);
            }
            catch (Exception exception)
                when (usage == "decision"
                    && exception
                        is UnknownModelException
                            or InvalidDataException
                            or InvalidOperationException
                            or ArgumentException
                )
            {
                if (_warnedDecisionProfiles.Add(profileName))
                {
                    Notify(
                        $"decision profile '{profileName}' is unavailable; the decision layer is off: {exception.Message}",
                        NoticeLevel.Warning
                    );
                }
            }
        }

        // The decision profile is configured in the decisions block, not as a usage; every
        // frontend resolves it through the same shared seam.
        if (
            DecisionUsageModels.ResolveDecisionModel(
                _settings,
                () => (_auth, _providerEntries),
                _catalog,
                _environment,
                _factory,
                notice => Notify(notice.Message, notice.Level),
                _warnedDecisionProfiles,
                SessionRequestContext()
            ) is
            { } decisionModel
        )
        {
            // A resolution that lands on a profile another usage already carries shares that
            // client, the one-client-per-profile rule the composition cache keeps.
            usages["decision"] =
                usages.Values.FirstOrDefault(client =>
                    client.ProfileName == decisionModel.ProfileName
                ) ?? decisionModel;
        }

        return usages;

        UsageModelClient GetClient(ResolvedModelProfile profile)
        {
            string key = profile.Profile.Name;
            if (clientsByProfile.TryGetValue(key, out UsageModelClient? existing))
            {
                return existing;
            }

            var client = new UsageModelClient(
                new DeferredChatClient(() => CreateUsageClient(profile)),
                profile.Profile.Name,
                profile.Provider.Name,
                profile.Model
            );
            clientsByProfile.Add(key, client);
            return client;
        }
    }

    private IReadOnlyDictionary<string, UsageModelClient>? ResolveRoutingProfiles() =>
        DecisionUsageModels.ResolveRoutingProfiles(
            _settings,
            _providerEntries,
            _auth,
            _catalog,
            _environment,
            _factory,
            SessionRequestContext()
        );

    private IChatClient CreateUsageClient(ResolvedModelProfile profile)
    {
        RequestContext? context = SessionRequestContext();
        return DecisionUsageModels.CreateUsageClient(
            profile,
            _auth,
            _environment,
            _factory,
            context
        );
    }
}
