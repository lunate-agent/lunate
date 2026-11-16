using System.Text.Json;
using Lunate.Ai;
using Lunate.Ai.Decisions;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

internal static class DecisionClientResolver
{
    public static IDecisionClient Resolve(IChatClient client, string method)
    {
        if (method == "native")
        {
            if (client.GetService(typeof(IDecisionClient)) is IDecisionClient decisionClient)
            {
                return decisionClient;
            }
            if (client.GetService(typeof(INativeDecisionClient)) is INativeDecisionClient legacy)
            {
                return new LegacyNativeDecisionClient(legacy);
            }
        }

        return new DecisionClientBuilder().UseChatClient(client).Build();
    }

    public static bool HasNativeClient(IChatClient client) =>
        client.GetService(typeof(IDecisionClient)) is IDecisionClient
        || client.GetService(typeof(INativeDecisionClient)) is INativeDecisionClient;

    public static bool SupportsImageInput(ModelInfo model) =>
        model.Capabilities?.InputModalities?.Contains("image") ?? model.SupportsVision;

    private sealed class LegacyNativeDecisionClient(INativeDecisionClient legacy) : IDecisionClient
    {
        public async Task<DecisionResult> GetDecisionAsync(
            DecisionRequest request,
            DecisionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            string? failure = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string response = await legacy
                    .GetDecisionAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    return DecisionResponseParser.Parse(
                        response,
                        request,
                        options?.ModelId ?? request.ModelId ?? "unknown",
                        "native"
                    );
                }
                catch (InvalidDataException exception)
                {
                    failure = exception.Message;
                }
                catch (JsonException)
                {
                    failure = "response is not valid JSON";
                }
            }

            throw new InvalidDataException(
                $"Decision for model '{options?.ModelId ?? request.ModelId ?? "unknown"}' failed after retry: {failure ?? "response did not match the decision contract"}."
            );
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(INativeDecisionClient) ? legacy : null;
    }
}
