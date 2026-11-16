using System.Net;
using Lunate.Ai;
using Lunate.Ai.Decisions;
using Lunate.Ai.SystemOne;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// A real System One decision transport over a handler that answers every request the same way, so a
/// test drives the harness through the native path instead of a scripted chat client.
/// </summary>
internal sealed class SystemOneTransport : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SystemOneRecordingHandler _handler;

    private SystemOneTransport(SystemOneRecordingHandler handler)
    {
        _handler = handler;
        _httpClient = new HttpClient(handler);
        Client = new SystemOneChatFacade(
            "typesafe/jev-1.14",
            new SystemOneDecisionClient(
                _httpClient,
                new SystemOnePreset(new Uri("https://example.test/api/v1/systemone"))
            )
        );
    }

    public IChatClient Client { get; }

    public int RequestCount => _handler.RequestCount;

    public static SystemOneTransport Start(string body, HttpStatusCode statusCode) =>
        new(new SystemOneRecordingHandler(body, statusCode));

    /// <summary>A usage model entry on the native decision path for this transport.</summary>
    public UsageModelClient DecisionProfile() =>
        new(
            Client,
            "decision-profile",
            "systemone",
            new ModelInfo("typesafe/jev-1.14", "systemone", null, 32000, SupportsTools: false)
            {
                ApiType = "native",
            }
        );

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}

/// <summary>A native decision client that fails the way an unexpected bug does, not the way a provider does.</summary>
internal sealed class ThrowingDecisionClient : IDecisionClient
{
    public Task<DecisionResult> GetDecisionAsync(
        DecisionRequest request,
        DecisionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => throw new InvalidOperationException("the transport stream ended unexpectedly");

    public object? GetService(Type serviceType) => serviceType.IsInstanceOfType(this) ? this : null;
}
