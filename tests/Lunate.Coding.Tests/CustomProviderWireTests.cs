using System.Net;
using System.Text;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The custom provider's placement round-trips: what <c>auth login --custom</c> writes into
/// settings is what the wire carries. These tests drive the stored entry through the catalog and
/// the real client factory onto a capturing transport — the same slice-2 wire technique the
/// provider wire tests use, replayed here from the settings file.
/// </summary>
public sealed class CustomProviderWireTests : IDisposable
{
    private const string Secret = "sk-custom-wire-secret";
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task An_x_api_key_placement_round_trips_from_settings_onto_the_wire()
    {
        using var wire = new CapturedWire(OpenAiChatCompletion);
        ModelInfo model = StoredModel(
            ProviderApiFamily.OpenAiChat,
            CredentialPlacement.XApiKey
        );

        IChatClient client = wire.Factory().Create(model);
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(wire.Last);
        Assert.Equal([Secret], wire.Last.Headers.GetValues("x-api-key"));
        Assert.Null(wire.HeaderValuesOrNull("Authorization"));
    }

    [Fact]
    public async Task A_bearer_placement_round_trips_from_settings_onto_the_wire()
    {
        using var wire = new CapturedWire(OpenAiChatCompletion);
        ModelInfo model = StoredModel(ProviderApiFamily.OpenAiChat, CredentialPlacement.Bearer);

        IChatClient client = wire.Factory().Create(model);
        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(wire.Last);
        Assert.Equal([$"Bearer {Secret}"], wire.Last.Headers.GetValues("Authorization"));
        Assert.False(wire.Last.Headers.Contains("x-api-key"));
    }

    /// <summary>
    /// Writes the provider through the one settings writer and resolves it back through the
    /// catalog the session uses, so the model the factory builds a client for is the stored
    /// entry's own rendering.
    /// </summary>
    private ModelInfo StoredModel(ProviderApiFamily family, CredentialPlacement placement)
    {
        string settingsPath = _temp.File("settings.json");
        var provider = new CustomProvider(
            "relay",
            new Uri("https://relay.example/v1"),
            "claude-wire",
            "relay/claude-wire",
            family,
            placement
        );
        SettingsStore.WriteProvider(provider, settingsPath);

        AgentSettings settings = SettingsStore.Resolve(settingsPath, _ => null);
        ModelCatalog catalog = HarnessFactory.LoadCatalog(settings, _temp.File("models.json"));
        return catalog.Find(provider.CatalogName)
            ?? throw new InvalidOperationException("the stored provider is not in the catalog");
    }

    private const string OpenAiChatCompletion = """
        {
            "id": "chatcmpl-wire",
            "object": "chat.completion",
            "created": 0,
            "model": "claude-wire",
            "choices": [
                {
                    "index": 0,
                    "message": { "role": "assistant", "content": "pong" },
                    "finish_reason": "stop"
                }
            ],
            "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
        }
        """;

    /// <summary>The capturing transport the SDK-built chain finally sends through.</summary>
    private sealed class CapturedWire : IDisposable
    {
        private readonly List<HttpRequestMessage> _requests = [];
        private readonly string _response;
        private bool _disposed;

        public CapturedWire(string response) => _response = response;

        public HttpRequestMessage? Last => _requests.LastOrDefault();

        public IEnumerable<string>? HeaderValuesOrNull(string name) =>
            Last is { } last && last.Headers.TryGetValues(name, out IEnumerable<string>? values)
                ? values
                : null;

        public ChatClientFactory Factory()
        {
            ChatClientFactory factory = new(
                NullLoggerFactory.Instance,
                enableOpenTelemetry: false,
                namedKeySource: key => key == "relay" ? Secret : null
            );
            factory.BaseTransportFactory = () => new CaptureHandler(this, _response);
            return factory;
        }

        public void Dispose() => _disposed = true;

        private sealed class CaptureHandler(CapturedWire wire, string response) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                if (!wire._disposed)
                {
                    wire._requests.Add(request);
                }

                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(response, Encoding.UTF8, "application/json"),
                    }
                );
            }
        }
    }
}
