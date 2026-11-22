using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace NodeRemoteExtension.Tests;

/// <summary>
/// A one-reply chat client: the harness streams the reply as the turn's assistant message, so a
/// real turn runs end to end without a provider or a network.
/// </summary>
internal sealed class SingleReplyChatClient(string reply = "ok") : IChatClient
{
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent(reply)])
        {
            FinishReason = ChatFinishReason.Stop,
        };
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
