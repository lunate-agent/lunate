using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The configured-state check the wizard trigger, the welcome header and <c>lunate setup</c> share
/// (spec: The configured-state check). Reproduced from configuration alone.
/// </summary>
public sealed class SetupStateTests
{
    [Fact]
    public void Configured_machine_does_not_apply()
    {
        ProviderEntry entry = Entry("primary");
        AgentSettings settings = new()
        {
            Profiles = Profiles(("chat-main", "primary", "chat-one")),
            Usages = Usages(("chat", "chat-main")),
        };

        Assert.Equal(SetupState.Reason.NotApplicable, SetupState.Check(settings, [entry]));
    }

    [Fact]
    public void Empty_provider_store_applies()
    {
        AgentSettings settings = new()
        {
            Profiles = Profiles(("chat-main", "primary", "chat-one")),
            Usages = Usages(("chat", "chat-main")),
        };

        Assert.Equal(SetupState.Reason.NoProvider, SetupState.Check(settings, []));
    }

    [Fact]
    public void Provider_without_a_chat_usage_applies()
    {
        AgentSettings settings = new()
        {
            Profiles = Profiles(("chat-main", "primary", "chat-one")),
        };

        Assert.Equal(
            SetupState.Reason.NoChatProfile,
            SetupState.Check(settings, [Entry("primary")])
        );
    }

    [Fact]
    public void Chat_usage_bound_to_an_absent_profile_applies()
    {
        AgentSettings settings = new()
        {
            Profiles = Profiles(("chat-main", "primary", "chat-one")),
            Usages = Usages(("chat", "missing")),
        };

        Assert.Equal(
            SetupState.Reason.NoChatProfile,
            SetupState.Check(settings, [Entry("primary")])
        );
    }

    [Fact]
    public void Model_default_resolves_the_chat_usage()
    {
        AgentSettings settings = new()
        {
            Profiles = Profiles(("chat-main", "primary", "chat-one")),
            Usages = Usages(("chat", "missing")),
            Model = "openai/chat-one",
        };

        Assert.Equal(
            SetupState.Reason.NotApplicable,
            SetupState.Check(settings, [Entry("primary")])
        );
    }

    [Fact]
    public void Fresh_machine_with_no_configuration_applies()
    {
        AgentSettings settings = new();

        Assert.Equal(SetupState.Reason.NoProvider, SetupState.Check(settings, []));
    }

    [Fact]
    public void Blank_model_default_is_not_a_default()
    {
        AgentSettings settings = new() { Model = "   " };

        Assert.Equal(SetupState.Reason.NoProvider, SetupState.Check(settings, []));
        Assert.Equal(
            SetupState.Reason.NoChatProfile,
            SetupState.Check(settings, [Entry("primary")])
        );
    }

    private static ProviderEntry Entry(string name)
    {
        bool valid = ProviderEntry.TryCreate(
            name,
            "openai",
            new Uri("https://provider.example.test/v1"),
            $"auth:{name}",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );

        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }

    private static IReadOnlyDictionary<string, ModelProfile> Profiles(
        params (string Name, string Provider, string Model)[] entries
    )
    {
        var profiles = new Dictionary<string, ModelProfile>(StringComparer.Ordinal);
        foreach ((string name, string provider, string model) in entries)
        {
            profiles.Add(name, new ModelProfile(name, provider, model));
        }

        return profiles;
    }

    private static IReadOnlyDictionary<string, string> Usages(
        params (string Usage, string Profile)[] entries
    )
    {
        var usages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string usage, string profile) in entries)
        {
            usages.Add(usage, profile);
        }

        return usages;
    }
}
