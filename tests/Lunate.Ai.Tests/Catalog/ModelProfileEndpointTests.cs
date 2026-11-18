using Lunate.TestSupport;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// A discovered per-model <c>endpoint</c> survives profile resolution unless the provider entry
/// declares its own URL for the resolved family: the entry is the user's explicit routing intent,
/// and the discovered endpoint is the fallback the catalog's own entry carries. Both cases go
/// through the real resolver and client factory onto the wire.
/// </summary>
[Collection(ChatClientFactoryEnvCollection.Name)]
public sealed class ModelProfileEndpointTests
{
    private const string SessionId = "discovery-endpoint-session";

    [Fact]
    public async Task A_discovered_endpoint_is_kept_when_the_entry_declares_no_family_url()
    {
        ModelCatalog catalog = DiscoveredCatalog();
        ProviderEntry provider = Provider(familyUrl: null);

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            new ModelProfile("chat", "go-proxy", "opencode-go/discovered-messages"),
            [provider],
            catalog
        );

        Assert.Equal(new Uri("https://relay.example/go/anthropic/v1"), resolved.Model.Endpoint);

        CapturingWire wire = CapturingWire.Anthropic();
        IChatClient client = wire.Factory().Create(resolved.Model, new RequestContext(SessionId));
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "https://relay.example/go/anthropic/v1/messages",
            wire.Last!.RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    public async Task A_provider_family_url_still_wins_over_the_discovered_endpoint()
    {
        ModelCatalog catalog = DiscoveredCatalog();
        ProviderEntry provider = Provider(
            familyUrl: new Uri("https://override.example/go/anthropic/v1")
        );

        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            new ModelProfile("chat", "go-proxy", "opencode-go/discovered-messages"),
            [provider],
            catalog
        );

        Assert.Equal(new Uri("https://override.example/go/anthropic/v1"), resolved.Model.Endpoint);

        CapturingWire wire = CapturingWire.Anthropic();
        IChatClient client = wire.Factory().Create(resolved.Model, new RequestContext(SessionId));
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(
            "https://override.example/go/anthropic/v1/messages",
            wire.Last!.RequestUri!.AbsoluteUri
        );
    }

    private static ModelCatalog DiscoveredCatalog() =>
        ModelCatalog.Load(
            Path.Combine(
                RepositoryRoot.Find(),
                "tests",
                "fixtures",
                "discovery",
                "discovered-endpoint-models.json"
            )
        );

    /// <summary>
    /// A provider entry of the OpenCode Go kind with only the chat shorthand declared: the
    /// Anthropic-messages family has no entry URL unless <paramref name="familyUrl"/> overrides it.
    /// </summary>
    private static ProviderEntry Provider(Uri? familyUrl)
    {
        IReadOnlyDictionary<ProviderApiFamily, Uri>? familyUrls = familyUrl is null
            ? null
            : new Dictionary<ProviderApiFamily, Uri>
            {
                [ProviderApiFamily.AnthropicMessages] = familyUrl,
            };
        bool valid = ProviderEntry.TryCreate(
            "go-proxy",
            "opencode-go",
            new Uri("https://proxy.example/go/openai/v1"),
            "env:GO_PROXY_KEY",
            headers: null,
            region: null,
            familyUrls,
            modelsUrl: null,
            out ProviderEntry? provider,
            out string? error
        );

        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }
}
