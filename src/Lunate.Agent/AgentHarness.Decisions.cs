using System.Globalization;
using System.Text.Json;
using Lunate.Ai;
using Lunate.Contracts;
using Microsoft.Extensions.AI;
using DecisionTransportException = Lunate.Ai.Decisions.DecisionTransportException;

namespace Lunate.Agent;

public sealed partial class AgentHarness
{
    private async Task<DecisionResponse> EvaluateToolDecisionAsync(
        string runId,
        DecisionRequest request,
        AgentEventChannel channel,
        CancellationToken ct
    )
    {
        UsageModelClient? model = ResolveUsageModel("decision");
        if (model is null)
        {
            throw new InvalidOperationException(
                "The decision profile is unavailable. Continue without this advice or configure a decision profile."
            );
        }

        bool native =
            model.Model.ApiType == "native" && DecisionClientResolver.HasNativeClient(model.Client);
        if (!native)
        {
            RequireCapability(
                model.Model,
                model.Model.Capabilities?.Features,
                "structured-output",
                "feature"
            );
        }

        string modelId = model.Model.Id;
        string method = native ? "native" : "adapter";
        try
        {
            DecisionResponse response = await DecisionEngine.EvaluateAsync(
                request,
                model.Client,
                modelId,
                method,
                ct,
                (usage, resolvedModelId, decisionMethod, probability, providerCost) =>
                    AccumulateUsage(
                        runId,
                        channel,
                        usage,
                        resolvedModelId,
                        UsageAttribution("decision", model, decisionMethod, probability),
                        providerCost
                    ),
                supportsImages: DecisionClientResolver.SupportsImageInput(model.Model)
            );

            string? stateWarning = DecisionStateSizeWarning(request.State);
            foreach ((string questionId, DecisionAnswer answer) in response.Results)
            {
                channel.Emit(
                    new DecisionEvaluated(
                        runId,
                        answer.ModelId,
                        answer.Method,
                        answer.Answer,
                        answer.Probability
                    )
                    {
                        QuestionId = questionId,
                        Answer = answer.Answer,
                        Warning = stateWarning,
                    }
                );
                stateWarning = null;
            }

            return response;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            foreach (DecisionQuestion question in request.Questions)
            {
                channel.Emit(
                    new DecisionFailed(runId, modelId, exception.Message)
                    {
                        QuestionId = question.Id,
                    }
                );
            }

            throw;
        }
    }

    /// <summary>
    /// The reason a failed decision exchange records. A native transport's own precise message (a
    /// 422 field, a state past the model's budget) is kept, redacted against the session's known
    /// credential values; an unexpected exception stays generic so it is never mistaken for a
    /// provider refusal.
    /// </summary>
    private string DecisionFailureReason(Exception exception)
    {
        string reason =
            exception is DecisionTransportException ? exception.Message : "provider request failed";
        return _options.Session?.Redactor.Redact(reason) ?? reason;
    }

    private static string? DecisionStateSizeWarning(JsonElement state)
    {
        long estimatedTokens = state.GetRawText().Length / 4L;
        if (estimatedTokens <= 28_000)
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Decision state is estimated above {estimatedTokens:N0} tokens (4 characters per token); it exceeds the default 32,000-token per-question budget. Long context is opt-in and can be slower and less accurate."
        );
    }
}
