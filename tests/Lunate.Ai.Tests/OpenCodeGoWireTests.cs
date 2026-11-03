using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// OpenCode Go's wire: one provider whose models ride four native APIs. The declared
/// <c>x-opencode-session</c> header resolves over the session the run executes in — the parent's
/// id on a top-level call, a child run's own id once that run activates its flow, never the
/// parent's — and each family routes the URL its preset declares.
/// </summary>
public sealed class OpenCodeGoWireTests
{
    private const string OpenCodeApiKeyVariable = "OPENCODE_API_KEY";

    internal static readonly ModelInfo GoChatModel = new("beam", "opencode-go", null, 8_192, true)
    {
        ApiFamily = "openai-chat",
    };

    private static readonly ChatMessage Prompt = new(ChatRole.User, "Hi");

    [Theory]
    [InlineData("openai-chat")]
    [InlineData("anthropic-messages")]
    public async Task A_family_model_sends_the_family_url_that_preset_declares(string family)
    {
        using var environment = new EnvironmentScope((OpenCodeApiKeyVariable, "wire-probe-key"));
        CapturingWire wire =
            family == "anthropic-messages" ? CapturingWire.Anthropic() : CapturingWire.OpenAi();
        ChatClientFactory factory = wire.Factory();
        var model = GoChatModel with { ApiFamily = family, Id = "wire-model" };

        IChatClient client = factory.Create(model, new RequestContext("wire-probe-session"));
        await client.GetResponseAsync(
            [Prompt],
            options: null,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(wire.Last);
        string expected =
            family == "anthropic-messages"
                ? "https://opencode.ai/inference/go/anthropic/v1/messages"
                : "https://opencode.ai/inference/go/openai/v1/chat/completions";
        Assert.Equal(expected, wire.Last.RequestUri!.AbsoluteUri);
        Assert.StartsWith("lunate/", wire.Last.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task The_session_header_names_the_parent_then_the_child_run_activated_on_the_flow()
    {
        using var environment = new EnvironmentScope((OpenCodeApiKeyVariable, "wire-probe-key"));
        CapturingWire wire = CapturingWire.OpenAi();
        IChatClient client = wire.Factory()
            .Create(GoChatModel, new RequestContext("session-parent"));

        // No ambient run: the context the factory call carried names the header.
        await client.GetResponseAsync(
            [Prompt],
            options: null,
            TestContext.Current.CancellationToken
        );
        // A child run activates its own pair on its own flow; the run in flight wins over the
        // context the client was built with, so a long-lived client names the child while the
        // child's harness runs inside it.
        using (RequestContext.Activate("session-child", "run-child"))
        {
            await client.GetResponseAsync(
                [Prompt],
                options: null,
                TestContext.Current.CancellationToken
            );
        }

        IReadOnlyList<IReadOnlyList<string>?> expected =
        [
            ["session-parent"],
            ["session-child"],
        ];
        Assert.Equal(expected, wire.ValuesAcrossRequests("x-opencode-session"));
    }
}
