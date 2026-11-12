using System.Globalization;
using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

public sealed class ReasoningParametersTests
{
    private static ModelInfo Model(
        string? style,
        string provider = "openai",
        string? family = null
    ) =>
        new("test-model", provider, null, 1, true, AuthRef: null, Reasoning: style)
        {
            ApiFamily = family,
        };

    [Theory]
    [InlineData("effort")]
    [InlineData("budget")]
    [InlineData("inline")]
    [InlineData(null)]
    public void Off_sends_no_reasoning_parameters(string? style)
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model(style), ThinkingLevel.Off);

        Assert.Null(options.Reasoning);
        Assert.Null(options.RawRepresentationFactory);
        Assert.Null(options.MaxOutputTokens);
    }

    [Theory]
    [InlineData(ThinkingLevel.Low)]
    [InlineData(ThinkingLevel.Medium)]
    [InlineData(ThinkingLevel.High)]
    public void A_model_without_a_declared_style_sends_nothing(ThinkingLevel level)
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model(style: null), level);

        Assert.Null(options.Reasoning);
        Assert.Null(options.RawRepresentationFactory);
        Assert.Null(options.MaxOutputTokens);
    }

    [Theory]
    [InlineData(ThinkingLevel.Low, ReasoningEffort.Low)]
    [InlineData(ThinkingLevel.Medium, ReasoningEffort.Medium)]
    [InlineData(ThinkingLevel.High, ReasoningEffort.High)]
    public void Effort_maps_the_level_to_the_openai_compatible_reasoning_effort(
        ThinkingLevel level,
        ReasoningEffort expected
    )
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model("effort"), level);

        Assert.Equal(expected, options.Reasoning?.Effort);
        Assert.Null(options.RawRepresentationFactory);
    }

    [Theory]
    [InlineData(ThinkingLevel.Low, 2_048L)]
    [InlineData(ThinkingLevel.Medium, 8_192L)]
    [InlineData(ThinkingLevel.High, 32_768L)]
    public void Budget_maps_the_level_to_extended_thinking_with_an_explicit_budget(
        ThinkingLevel level,
        long expectedBudget
    )
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model("budget", provider: "anthropic"), level);

        MessageCreateParams parameters = Assert.IsType<MessageCreateParams>(
            options.RawRepresentationFactory!(null!)
        );
        ThinkingConfigEnabled thinking = Assert.IsType<ThinkingConfigEnabled>(
            parameters.Thinking!.Value
        );
        Assert.Equal(expectedBudget, thinking.BudgetTokens);
        Assert.Equal("test-model", parameters.Model.Raw());
        Assert.Equal((long)options.MaxOutputTokens!, parameters.MaxTokens);
        Assert.True(parameters.MaxTokens > expectedBudget);
    }

    [Fact]
    public void Budget_raises_an_explicit_max_tokens_that_leaves_no_room_for_the_budget()
    {
        ChatOptions options = new() { MaxOutputTokens = 1_000 };

        ReasoningParameters.Apply(
            options,
            Model("budget", provider: "anthropic"),
            ThinkingLevel.High
        );

        Assert.True(options.MaxOutputTokens > 32_768);
    }

    [Fact]
    public void Budget_keeps_an_explicit_max_tokens_that_already_leaves_room()
    {
        ChatOptions options = new() { MaxOutputTokens = 100_000 };

        ReasoningParameters.Apply(
            options,
            Model("budget", provider: "anthropic"),
            ThinkingLevel.High
        );

        Assert.Equal(100_000, options.MaxOutputTokens);
    }

    [Theory]
    [InlineData("openai", null)]
    [InlineData("custom", "openai-chat")]
    [InlineData("custom", "openai-responses")]
    [InlineData("custom", "google")]
    public void A_model_declared_budget_on_a_non_anthropic_family_sends_effort(
        string provider,
        string? family
    )
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model("budget", provider, family), ThinkingLevel.High);

        Assert.Equal(ReasoningEffort.High, options.Reasoning?.Effort);
        Assert.Null(options.RawRepresentationFactory);
        Assert.Null(options.MaxOutputTokens);
    }

    [Theory]
    [InlineData("anthropic", null)]
    [InlineData("custom", "anthropic-messages")]
    public void A_model_declared_effort_on_the_anthropic_family_sends_a_budget(
        string provider,
        string? family
    )
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model("effort", provider, family), ThinkingLevel.High);

        MessageCreateParams parameters = Assert.IsType<MessageCreateParams>(
            options.RawRepresentationFactory!(null!)
        );
        ThinkingConfigEnabled thinking = Assert.IsType<ThinkingConfigEnabled>(
            parameters.Thinking!.Value
        );
        Assert.Equal(32_768L, thinking.BudgetTokens);
        Assert.Null(options.Reasoning);
    }

    [Fact]
    public void An_unresolvable_family_falls_back_to_the_declared_style()
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(
            options,
            Model("budget", provider: "no-such-provider"),
            ThinkingLevel.High
        );

        MessageCreateParams parameters = Assert.IsType<MessageCreateParams>(
            options.RawRepresentationFactory!(null!)
        );
        Assert.IsType<ThinkingConfigEnabled>(parameters.Thinking!.Value);
        Assert.Null(options.Reasoning);
    }

    [Theory]
    [InlineData("effort", "anthropic", null, ThinkingLevel.High, "thinking budget 32768 tokens")]
    [InlineData("budget", "openai", null, ThinkingLevel.Medium, "reasoning effort medium")]
    [InlineData("budget", "custom", "openai-chat", ThinkingLevel.Low, "reasoning effort low")]
    public void Describe_follows_the_resolved_family(
        string style,
        string provider,
        string? family,
        ThinkingLevel level,
        string expected
    ) =>
        Assert.Equal(expected, ReasoningParameters.Describe(Model(style, provider, family), level));

    [Fact]
    public void Inline_sends_no_provider_parameter()
    {
        ChatOptions options = new();

        ReasoningParameters.Apply(options, Model("inline"), ThinkingLevel.High);

        Assert.Null(options.Reasoning);
        Assert.Null(options.RawRepresentationFactory);
        Assert.Null(options.MaxOutputTokens);
    }

    [Theory]
    [InlineData(ThinkingLevel.Low, "reasoning effort low")]
    [InlineData(ThinkingLevel.Medium, "reasoning effort medium")]
    [InlineData(ThinkingLevel.High, "reasoning effort high")]
    public void Describe_names_the_effort_an_effort_model_would_send(
        ThinkingLevel level,
        string expected
    ) => Assert.Equal(expected, ReasoningParameters.Describe(Model("effort"), level));

    [Theory]
    [InlineData(ThinkingLevel.Low, "thinking budget 2048 tokens")]
    [InlineData(ThinkingLevel.Medium, "thinking budget 8192 tokens")]
    [InlineData(ThinkingLevel.High, "thinking budget 32768 tokens")]
    public void Describe_names_the_budget_a_budget_model_would_send(
        ThinkingLevel level,
        string expected
    ) => Assert.Equal(expected, ReasoningParameters.Describe(Model("budget", "anthropic"), level));

    [Theory]
    [InlineData("effort")]
    [InlineData("budget")]
    public void Describe_reads_off_as_sending_nothing(string style) =>
        Assert.Equal(
            "no reasoning parameters",
            ReasoningParameters.Describe(Model(style), ThinkingLevel.Off)
        );

    [Fact]
    public void Describe_reads_a_model_without_a_style_as_having_no_reasoning_support() =>
        Assert.Equal(
            "no reasoning support",
            ReasoningParameters.Describe(Model(null), ThinkingLevel.High)
        );

    [Fact]
    public void Describe_reads_inline_as_the_models_own_stream() =>
        Assert.Equal(
            "inline thinking (no provider parameter)",
            ReasoningParameters.Describe(Model("inline"), ThinkingLevel.High)
        );

    [Fact]
    public void Describe_formats_the_budget_invariantly_under_a_machine_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-AT");

            Assert.Equal(
                "thinking budget 8192 tokens",
                ReasoningParameters.Describe(Model("budget", "anthropic"), ThinkingLevel.Medium)
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
