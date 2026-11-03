namespace Lunate.Ai.Tests;

/// <summary>The family write contract: wire names, exact-match parsing and named refusals.</summary>
public sealed class ProviderApiFamilyTests
{
    [Theory]
    [InlineData("openai-chat", ProviderApiFamily.OpenAiChat)]
    [InlineData("openai-responses", ProviderApiFamily.OpenAiResponses)]
    [InlineData("anthropic-messages", ProviderApiFamily.AnthropicMessages)]
    [InlineData("google", ProviderApiFamily.Google)]
    public void A_wire_name_parses_to_its_family(string wireName, ProviderApiFamily family)
    {
        Assert.True(ProviderApiFamilies.TryParse(wireName, out ProviderApiFamily parsed));
        Assert.Equal(family, parsed);
        Assert.Equal(wireName, ProviderApiFamilies.Name(family));
    }

    [Theory]
    [InlineData("OPENAI-CHAT")]
    [InlineData("openai")]
    [InlineData("responses")]
    [InlineData("unknown")]
    [InlineData("")]
    public void An_unknown_value_is_refused(string wireName)
    {
        Assert.False(ProviderApiFamilies.TryParse(wireName, out _));
        Assert.Contains(
            wireName,
            ProviderApiFamilies.Unsupported(wireName, "test"),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "openai-responses",
            ProviderApiFamilies.Supported(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void The_supported_list_names_every_family_in_declaration_order()
    {
        Assert.Equal(
            "openai-chat, openai-responses, anthropic-messages, google",
            ProviderApiFamilies.Supported()
        );
    }

    [Fact]
    public void Names_for_a_family_set_is_deterministic_and_deduplicated()
    {
        string listed = ProviderApiFamilies.NamesFor([
            ProviderApiFamily.Google,
            ProviderApiFamily.OpenAiChat,
            ProviderApiFamily.Google,
        ]);

        Assert.Equal("openai-chat, google", listed);
    }
}
