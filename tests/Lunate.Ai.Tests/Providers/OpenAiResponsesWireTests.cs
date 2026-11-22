using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// The Responses family's wire: what a request through the Responses transport actually carries.
/// The tests drive the factory end to end against a scripted Responses wire, so the assertions
/// see the same wire a provider sees. Payloads are the Responses API's SSE event sequences.
/// </summary>
public sealed class OpenAiResponsesWireTests
{
    private const string OpenAiApiKeyVariable = "OPENAI_API_KEY";

    [Fact]
    public async Task The_responses_request_carries_the_lunate_user_agent_and_the_bearer_credential()
    {
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "responses-probe-key"));
        var handler = new ScriptedResponsesHandler(OpenAiResponsesWirePayloads.TextTurn);
        ChatClientFactory factory = ResponsesFactory(handler);
        IChatClient client = factory.Create(ResponsesModel);

        await StreamAsync(
            client,
            new ChatOptions(),
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        RequestCapture capture = handler.Captures.Single();
        Assert.EndsWith(
            "/responses",
            capture.Request.RequestUri!.AbsolutePath,
            StringComparison.Ordinal
        );
        Assert.StartsWith(
            "lunate/",
            capture.Request.Headers.UserAgent.ToString(),
            StringComparison.Ordinal
        );
        Assert.Equal(
            "Bearer responses-probe-key",
            Assert.Single(capture.Request.Headers.GetValues("Authorization"))
        );
    }

    [Fact]
    public async Task The_responses_request_carries_the_entry_declared_headers()
    {
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "responses-probe-key"));
        var handler = new ScriptedResponsesHandler(OpenAiResponsesWirePayloads.TextTurn);
        ChatClientFactory factory = ResponsesFactory(handler);
        var model = ResponsesModel with
        {
            ProviderHeaders = new Dictionary<string, string> { ["x-probe"] = "{session.id}" },
        };

        IChatClient client = factory.Create(model, new RequestContext("responses-probe-session"));
        await StreamAsync(client, new ChatOptions(), new ChatMessage(ChatRole.User, "Hi"));

        Assert.Equal(
            ["responses-probe-session"],
            handler.Captures.Single().Request.Headers.GetValues("x-probe")
        );
    }

    [Fact]
    public async Task An_x_api_key_credential_placement_sends_the_key_once_over_the_responses_wire()
    {
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "responses-probe-key"));
        var handler = new ScriptedResponsesHandler(OpenAiResponsesWirePayloads.TextTurn);
        ChatClientFactory factory = ResponsesFactory(handler);

        IChatClient client = factory.Create(
            ResponsesModel with
            {
                CredentialPlacement = "x-api-key",
            }
        );
        await StreamAsync(client, new ChatOptions(), new ChatMessage(ChatRole.User, "Hi"));

        HttpRequestMessage request = handler.Captures.Single().Request;
        Assert.False(request.Headers.TryGetValues("Authorization", out IEnumerable<string>? _));
        Assert.Equal(["responses-probe-key"], request.Headers.GetValues("x-api-key"));
    }

    [Fact]
    public async Task A_streamed_response_streams_text_over_the_responses_sse()
    {
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "responses-probe-key"));
        var handler = new ScriptedResponsesHandler(OpenAiResponsesWirePayloads.TextTurn);
        ChatClientFactory factory = ResponsesFactory(handler);
        IChatClient client = factory.Create(ResponsesModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            new ChatOptions(),
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        Assert.Equal(
            "pong",
            Assert.Single(updates.SelectMany(update => update.Contents).OfType<TextContent>()).Text
        );
    }

    [Fact]
    public async Task A_streamed_tool_call_yields_a_function_call()
    {
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "responses-probe-key"));
        var handler = new ScriptedResponsesHandler(OpenAiResponsesWirePayloads.ToolCallTurn);
        ChatClientFactory factory = ResponsesFactory(handler);
        IChatClient client = factory.Create(ResponsesModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            ToolLoopOptions(),
            new ChatMessage(ChatRole.User, "Use the echo tool to echo the word ping.")
        );

        FunctionCallContent call = Assert.Single(
            updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>()
        );
        Assert.Equal("echo", call.Name);
        Assert.Equal("call_1", call.CallId);
    }

    [Fact]
    public async Task A_tool_result_flows_back_over_the_next_request_as_a_function_call_output()
    {
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "responses-probe-key"));
        var handler = new ScriptedResponsesHandler(
            OpenAiResponsesWirePayloads.ToolCallTurn,
            OpenAiResponsesWirePayloads.TextTurn
        );
        ChatClientFactory factory = ResponsesFactory(handler);
        IChatClient client = factory.Create(ResponsesModel);
        ChatMessage prompt = new(ChatRole.User, "Use the echo tool to echo the word ping.");

        List<ChatResponseUpdate> first = await StreamAsync(client, ToolLoopOptions(), prompt);
        await StreamAsync(
            client,
            ToolLoopOptions(),
            prompt,
            first.ToChatResponse().Messages[0],
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", "pong")])
        );

        string body = handler.Captures[^1].Body;
        Assert.Contains("\"type\":\"function_call_output\"", body, StringComparison.Ordinal);
        Assert.Contains("call_1", body, StringComparison.Ordinal);
        Assert.Contains("pong", body, StringComparison.Ordinal);
    }

    [Fact]
    public void A_responses_model_on_a_compatible_preset_routes_to_the_family_declared_url()
    {
        using var environment = new EnvironmentScope(("OPENCODE_API_KEY", "fixture-only-key"));
        ProviderPreset preset = ProviderPresets.Find("opencode-go")!;
        ChatClientFactory factory = new(
            new MarkingLoggerFactory(static () => { }),
            enableOpenTelemetry: false
        );
        var model = new ModelInfo("beam", "opencode-go", Endpoint: null, ContextWindow: 8192, true)
        {
            ApiFamily = "openai-responses",
        };

        IChatClient client = factory.Create(model);

        var metadata = (ChatClientMetadata)client.GetService(typeof(ChatClientMetadata))!;
        Assert.Equal(preset.UrlFor(ProviderApiFamily.OpenAiResponses), metadata.ProviderUri);
    }

    internal static readonly ModelInfo ResponsesModel = new(
        "gpt-wire",
        "openai",
        null,
        128_000,
        true
    )
    {
        ApiFamily = "openai-responses",
    };

    /// <summary>The tool loop's options: the echo tool both turns declare.</summary>
    internal static ChatOptions ToolLoopOptions() => new() { Tools = [new EchoProbeTool()] };

    private static ChatClientFactory ResponsesFactory(ScriptedResponsesHandler handler) =>
        new(new MarkingLoggerFactory(static () => { }), enableOpenTelemetry: false)
        {
            BaseTransportFactory = () => handler,
        };

    private static async Task<List<ChatResponseUpdate>> StreamAsync(
        IChatClient client,
        ChatOptions options,
        params ChatMessage[] messages
    )
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (
            ChatResponseUpdate update in client.GetStreamingResponseAsync(
                messages,
                options,
                TestContext.Current.CancellationToken
            )
        )
        {
            updates.Add(update);
        }

        return updates;
    }
}

/// <summary>The scripted Responses wire: serves each SSE payload once in order and keeps every
/// request — captured at send time, because the SDK disposes the content afterwards.</summary>
internal sealed class ScriptedResponsesHandler(params string[] payloads) : HttpMessageHandler
{
    private readonly List<RequestCapture> _captures = [];

    /// <summary>Every request that reached this wire, in order, with its body.</summary>
    public IReadOnlyList<RequestCapture> Captures => _captures;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        string body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _captures.Add(new RequestCapture(request, body));
        string payload = payloads[_captures.Count - 1];
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(payload))),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return response;
    }
}

/// <summary>One captured Responses request and the body bytes the transport read.</summary>
internal sealed record RequestCapture(HttpRequestMessage Request, string Body);

/// <summary>The tool the Responses wire probes declare: echoes "pong" whatever it receives.</summary>
internal sealed class EchoProbeTool : AIFunction
{
    private static readonly JsonElement Schema = JsonDocument
        .Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""")
        .RootElement.Clone();

    public override string Name => "echo";

    public override JsonElement JsonSchema => Schema;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken
    ) => new("pong");
}
