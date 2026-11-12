using System.Globalization;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace Lunate.Ai;

/// <summary>
/// Translates the session thinking level into provider parameters from the model's resolved
/// <see cref="ModelInfo.ApiFamily"/> (design of the reasoning-family-parameters change): the
/// <c>anthropic-messages</c> family maps to extended thinking with an explicit budget, the
/// <c>openai-chat</c>, <c>openai-responses</c> and <c>google</c> families map to the
/// OpenAI-compatible reasoning effort, and <c>inline</c> sends nothing (the model embeds the
/// thinking text in its stream). The declared <see cref="ModelInfo.Reasoning"/> style is metadata
/// and fallback: it decides the shape only when no family can be resolved. <see cref="ThinkingLevel.Off"/>
/// — and a model without a declared style — sends no reasoning parameters.
/// </summary>
public static class ReasoningParameters
{
    private const string EffortStyle = "effort";
    private const string BudgetStyle = "budget";
    private const string InlineStyle = "inline";
    private const string OffDescription = "no reasoning parameters";

    /// <summary>
    /// The output headroom kept above a thinking budget: <c>max_tokens</c> must be present and
    /// exceed the budget, so an explicit limit that leaves no room is raised by this much (the
    /// same headroom the Anthropic SDK reserves when it raises an absent limit itself).
    /// </summary>
    private const int OutputHeadroom = 1_024;

    public static void Apply(ChatOptions options, ModelInfo model, ThinkingLevel level)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(model);
        if (level == ThinkingLevel.Off)
        {
            return;
        }

        switch (WireStyle(model))
        {
            case EffortStyle:
                options.Reasoning = new() { Effort = EffortFor(level) };
                break;
            case BudgetStyle:
                ApplyBudget(options, model.WireId ?? model.Id, level);
                break;
        }
    }

    /// <summary>
    /// The wire parameter the resolved family sends. The declared style is metadata: an
    /// <c>inline</c> model embeds its reasoning in the text stream and a model with no declared
    /// style has no reasoning support, so neither sends a provider parameter. Otherwise the
    /// resolved family decides — Anthropic Messages takes a thinking budget, the OpenAI-compatible
    /// and Google families take the reasoning effort — and the declared <c>effort</c>/<c>budget</c>
    /// style is the fallback only when the family cannot be resolved.
    /// </summary>
    private static string? WireStyle(ModelInfo model)
    {
        if (model.Reasoning is InlineStyle)
        {
            return InlineStyle;
        }

        if (model.Reasoning is null)
        {
            return null;
        }

        return ResolvedFamily(model) switch
        {
            ProviderApiFamily.AnthropicMessages => BudgetStyle,
            ProviderApiFamily.OpenAiChat
            or ProviderApiFamily.OpenAiResponses
            or ProviderApiFamily.Google => EffortStyle,
            _ => model.Reasoning,
        };
    }

    /// <summary>
    /// The model's API family as the rest of the pipeline resolves it: the declared wire name
    /// first, then a provider whose preset serves exactly one chat family. Null when neither
    /// answers, so the declared style stays the fallback. The decision-only <c>systemone</c>
    /// family is never a chat family here.
    /// </summary>
    private static ProviderApiFamily? ResolvedFamily(ModelInfo model)
    {
        if (ProviderApiFamilies.TryParse(model.ApiFamily, out ProviderApiFamily declared))
        {
            return declared == ProviderApiFamily.SystemOne ? null : declared;
        }

        return
            ProviderPresets.Find(model.Provider) is { } preset
            && preset.ChatFamilies is [ProviderApiFamily only]
            ? only
            : null;
    }

    private static ReasoningEffort EffortFor(ThinkingLevel level) =>
        level switch
        {
            ThinkingLevel.Low => ReasoningEffort.Low,
            ThinkingLevel.Medium => ReasoningEffort.Medium,
            _ => ReasoningEffort.High,
        };

    private static long BudgetFor(ThinkingLevel level) =>
        level switch
        {
            ThinkingLevel.Low => 2_048,
            ThinkingLevel.Medium => 8_192,
            _ => 32_768,
        };

    /// <summary>
    /// The active model's mapping for the bare <c>/thinking</c> notice: the effort the resolved
    /// family would send, the token budget it would send, the inline convention, or that the
    /// model has no reasoning support. It follows the same family-first resolution as
    /// <see cref="Apply"/>, so the notice never describes a request the pipeline does not send.
    /// <see cref="ThinkingLevel.Off"/> reads as sending nothing. Numbers are formatted
    /// culture-invariantly.
    /// </summary>
    public static string Describe(ModelInfo model, ThinkingLevel level)
    {
        ArgumentNullException.ThrowIfNull(model);
        return WireStyle(model) switch
        {
            EffortStyle when level != ThinkingLevel.Off => "reasoning effort "
                + EffortFor(level).ToString().ToLowerInvariant(),
            BudgetStyle when level != ThinkingLevel.Off => "thinking budget "
                + BudgetFor(level).ToString(CultureInfo.InvariantCulture)
                + " tokens",
            EffortStyle or BudgetStyle => OffDescription,
            InlineStyle => "inline thinking (no provider parameter)",
            _ => "no reasoning support",
        };
    }

    /// <summary>
    /// Enables Anthropic extended thinking with the level's token budget. The request names the
    /// wire id, so a catalog id with a differing <see cref="ModelInfo.WireId"/> still reaches the
    /// provider. It carries an explicit <c>max_tokens</c> (an API requirement when thinking is on)
    /// clamped above the budget: an existing limit is raised when it leaves no room, otherwise it
    /// is kept.
    /// </summary>
    private static void ApplyBudget(ChatOptions options, string wireId, ThinkingLevel level)
    {
        long budget = BudgetFor(level);
        long maxTokens = Math.Max(options.MaxOutputTokens ?? 0, budget + OutputHeadroom);
        options.MaxOutputTokens = (int)maxTokens;
        options.RawRepresentationFactory = _ => CreateRequest(wireId, maxTokens, budget);
    }

    private static MessageCreateParams CreateRequest(string wireId, long maxTokens, long budget) =>
        new()
        {
            Model = wireId,
            MaxTokens = maxTokens,
            Messages = [],
            Thinking = new ThinkingConfigParam(new ThinkingConfigEnabled(budget)),
        };
}
