using System.Text.Json;
using Lunate.Ai.Decisions;

namespace Lunate.Agent;

/// <summary>Exposes an internal decision client through the stable native-decision contract.</summary>
internal sealed class NativeDecisionClientForwarder(IDecisionClient inner, string modelId)
    : INativeDecisionClient
{
    public async Task<string> GetDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        DecisionResult response = await inner
            .GetDecisionAsync(
                request with
                {
                    ModelId = request.ModelId ?? modelId,
                    // The legacy native contract leaves a yes/no question's answers implicit; the
                    // one decision model carries them, so the interface's rule is applied here.
                    Questions =
                    [
                        .. request.Questions.Select(question =>
                            question.Kind == DecisionQuestionKind.YesNo && question.Answers is null
                                ? question with
                                {
                                    Answers = ["yes", "no"],
                                }
                                : question
                        ),
                    ],
                },
                new DecisionOptions(ModelId: modelId),
                cancellationToken
            )
            .ConfigureAwait(false);

        Dictionary<string, object> results = new(StringComparer.Ordinal);
        foreach ((string id, DecisionAnswer answer) in response.Results)
        {
            Dictionary<string, object> mapped = new(StringComparer.Ordinal)
            {
                ["answer"] = answer.Answer,
                ["probabilities"] = answer.Distribution.ToDictionary(
                    item => item.Answer,
                    item => item.Probability,
                    StringComparer.Ordinal
                ),
            };
            if (answer.Box is { } box)
            {
                mapped["box"] = new
                {
                    top = box.Top,
                    bottom = box.Bottom,
                    left = box.Left,
                    right = box.Right,
                };
                mapped["confidence"] = answer.Confidence;
            }
            results.Add(id, mapped);
        }
        return JsonSerializer.Serialize(new { results });
    }
}
