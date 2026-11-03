using Lunate.Ai.Decisions;
using Lunate.Contracts;
using Microsoft.Extensions.AI;
using InternalDecisionAnswer = Lunate.Ai.Decisions.DecisionAnswer;
using InternalDecisionQuestion = Lunate.Ai.Decisions.DecisionQuestion;
using InternalDecisionQuestionKind = Lunate.Ai.Decisions.DecisionQuestionKind;

namespace Lunate.Agent;

internal static class DecisionEngine
{
    public static async Task<DecisionResponse> EvaluateAsync(
        Lunate.Contracts.DecisionRequest request,
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
            question.Kind == Lunate.Contracts.DecisionQuestionKind.Box
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
        Lunate.Ai.Decisions.DecisionRequest internalRequest = new(
            modelId,
            request.State,
            prepared.Select(question => ToInternalQuestion(question, questionCriteria)).ToArray()
        );
        IDecisionClient decisionClient = DecisionClientResolver.Resolve(client, method);
        DecisionResult result = await decisionClient
            .GetDecisionAsync(
                internalRequest,
                new DecisionOptions(ModelId: modelId),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.ChatUsageAttempts is { Count: > 0 } usages && recordUsage is not null)
        {
            InternalDecisionAnswer? first = result.Results.Values.FirstOrDefault();
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
            InternalDecisionAnswer? sample = result.Results.Values.FirstOrDefault();
            recordUsage(
                details,
                sample?.ModelId ?? modelId,
                sample?.Method,
                sample?.Probability,
                providerUsage.Cost
            );
        }

        Dictionary<string, Lunate.Contracts.DecisionAnswer> answers = new(StringComparer.Ordinal);
        foreach ((string id, InternalDecisionAnswer answer) in result.Results)
        {
            answers.Add(
                id,
                new Lunate.Contracts.DecisionAnswer(
                    answer.Answer,
                    answer.Probability,
                    answer
                        .Distribution.Select(item => new Lunate.Contracts.DecisionProbability(
                            item.Answer,
                            item.Probability
                        ))
                        .ToArray(),
                    answer.ModelId,
                    answer.Method
                )
                {
                    Confidence = answer.Confidence,
                    Box = answer.Box is { } box
                        ? new Lunate.Contracts.DecisionBox(
                            box.Top,
                            box.Bottom,
                            box.Left,
                            box.Right,
                            answer.Confidence
                        )
                        : null,
                }
            );
        }

        return new DecisionResponse(answers);
    }

    private static InternalDecisionQuestion ToInternalQuestion(
        PreparedDecisionQuestion prepared,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? questionCriteria
    )
    {
        Lunate.Contracts.DecisionQuestion question = prepared.Question;
        IReadOnlyDictionary<string, string>? criteria =
            questionCriteria is not null && questionCriteria.TryGetValue(question.Id, out var value)
                ? value
                : null;
        return new InternalDecisionQuestion(
            question.Id,
            question.Text,
            question.Kind switch
            {
                Lunate.Contracts.DecisionQuestionKind.YesNo => InternalDecisionQuestionKind.Noul,
                Lunate.Contracts.DecisionQuestionKind.Choice => InternalDecisionQuestionKind.Choice,
                Lunate.Contracts.DecisionQuestionKind.Score => InternalDecisionQuestionKind.Score,
                Lunate.Contracts.DecisionQuestionKind.Box => InternalDecisionQuestionKind.Box,
                _ => throw new InvalidOperationException(
                    "The validated question kind is unsupported."
                ),
            },
            prepared.Answers,
            Criteria: criteria,
            JsonPath: question.JsonPath,
            State: prepared.State
        );
    }
}
