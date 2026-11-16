using System.Text.Json;
using Lunate.Ai.Decisions;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

internal static class DecisionEngine
{
    public static async Task<DecisionResult> EvaluateAsync(
        DecisionRequest request,
        IChatClient client,
        string modelId,
        string method,
        CancellationToken cancellationToken = default,
        Action<UsageDetails, string, string?, double?, double?>? recordUsage = null,
        bool supportsImages = false,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? questionCriteria = null
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        bool hasBoxQuestion = request.Questions.Any(question =>
            question.Kind == DecisionQuestionKind.Box
        );
        if (hasBoxQuestion && !supportsImages)
        {
            // A box question is answered from an image; on a model without image input the
            // missing capability is named, per the decision-layer spec.
            throw new InvalidOperationException(
                $"Model '{modelId}' cannot decide box questions because it is missing required input modality 'image'. Use a vision-capable model for this request."
            );
        }
        if (DecisionRequestValidator.ContainsImagePart(request.State) && !supportsImages)
        {
            throw new InvalidOperationException(
                $"model '{modelId}' doesn't support required input modality 'image'."
            );
        }
        IReadOnlyList<PreparedDecisionQuestion> prepared = DecisionRequestValidator.Prepare(
            request
        );
        // The boundary validates once and hands the client the resolved per-question answer sets,
        // criteria and states — the one model the client, the parser and the consumers share.
        var clientRequest = new DecisionRequest(
            request.State,
            [.. prepared.Select(question => ToClientQuestion(question, questionCriteria))],
            modelId
        );
        IDecisionClient decisionClient = DecisionClientResolver.Resolve(client, method);
        DecisionResult result;
        try
        {
            result = await decisionClient
                .GetDecisionAsync(
                    clientRequest,
                    new DecisionOptions(ModelId: modelId),
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (DecisionUsageAttemptsException exception)
        {
            // Both attempts were off-contract but both were paid for: record their tokens before
            // the failure propagates, as 0.2.1 did.
            if (recordUsage is not null)
            {
                foreach (DecisionUsageAttempt attempt in exception.Attempts)
                {
                    recordUsage(attempt.Usage, attempt.ModelId ?? modelId, null, null, null);
                }
            }

            throw new InvalidDataException(exception.Message, exception);
        }

        if (result.ChatUsageAttempts is { Count: > 0 } usages && recordUsage is not null)
        {
            DecisionAnswer? first = result.Results.Values.FirstOrDefault();
            for (int index = 0; index < usages.Count; index++)
            {
                bool isFinalAttempt = index == usages.Count - 1;
                recordUsage(
                    usages[index],
                    first?.ModelId ?? modelId,
                    isFinalAttempt ? first?.Method : null,
                    isFinalAttempt ? first?.Probability : null,
                    null
                );
            }
        }
        else if (result.Usage is { } providerUsage && recordUsage is not null)
        {
            // The native transport's provider usage: the tokens travel in the usage details, the
            // provider-reported cost beside them, so the same accumulation path (session record,
            // run budget, /cost) serves both transports.
            var details = new UsageDetails
            {
                InputTokenCount = providerUsage.InputTokens,
                OutputTokenCount = providerUsage.OutputTokens,
            };
            DecisionAnswer? sample = result.Results.Values.FirstOrDefault();
            recordUsage(
                details,
                sample?.ModelId ?? modelId,
                sample?.Method,
                sample?.Probability,
                providerUsage.Cost
            );
        }

        return result;
    }

    private static DecisionQuestion ToClientQuestion(
        PreparedDecisionQuestion prepared,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? questionCriteria
    )
    {
        DecisionQuestion question = prepared.Question;
        IReadOnlyDictionary<string, string>? criteria =
            questionCriteria is not null && questionCriteria.TryGetValue(question.Id, out var value)
                ? value
                : null;
        return question with
        {
            Answers = prepared.Answers,
            Criteria = criteria,
            State = prepared.State,
        };
    }
}
