using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// The Google family's wire: what a request through the native Gemini transport actually carries.
/// The tests drive the factory end to end against a scripted Gemini wire, so the assertions see
/// the same wire a provider sees. Payloads are the Gemini SSE chunk shapes.
/// </summary>
[Collection(ChatClientFactoryEnvCollection.Name)]
public sealed class GoogleWireTests
{
    private const string GoogleApiKeyVariable = "GOOGLE_API_KEY";
    private const string Signature = "c2lnX2FfZnVuY3Rpb24=";

    [Fact]
    public async Task The_gemini_request_carries_the_lunate_agent_the_family_path_and_the_query_key()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        await StreamAsync(
            client,
            options: null,
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        RequestCapture capture = handler.Captures.Single();
        HttpRequestMessage request = capture.Request;
        Assert.EndsWith(
            "/v1beta/models/gemini-wire:streamGenerateContent",
            request.RequestUri!.AbsolutePath,
            StringComparison.Ordinal
        );
        Assert.Equal("alt=sse&key=gem-probe-key", request.RequestUri.Query.TrimStart('?'));
        Assert.StartsWith(
            "lunate/",
            request.Headers.UserAgent.ToString(),
            StringComparison.Ordinal
        );
        // The SDK's own key header is removed: the sentinel never reaches the provider, and the
        // declared query placement carries the real value.
        Assert.False(request.Headers.Contains("x-goog-api-key"));
        Assert.DoesNotContain("lunate-sentinel", capture.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_opencode_go_google_model_routes_to_the_family_declared_url()
    {
        using var environment = new EnvironmentScope(("OPENCODE_API_KEY", "go-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        var model = new ModelInfo(
            "gemini-beam",
            "opencode-go",
            Endpoint: null,
            ContextWindow: 8192,
            true
        )
        {
            ApiFamily = "google",
        };

        IChatClient client = factory.Create(model, new RequestContext("go-session"));
        await StreamAsync(client, options: null, new ChatMessage(ChatRole.User, "Hi"));

        HttpRequestMessage request = handler.Captures.Single().Request;
        Assert.EndsWith(
            "/v1beta/models/gemini-beam:streamGenerateContent",
            request.RequestUri!.AbsolutePath,
            StringComparison.Ordinal
        );
        Assert.Equal("opencode.ai", request.RequestUri.Host);
        Assert.Equal("Bearer go-probe-key", request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task A_declared_session_header_rides_the_gemini_transport()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        var model = GeminiModel with
        {
            ProviderHeaders = new Dictionary<string, string> { ["x-probe"] = "{session.id}" },
        };

        IChatClient client = factory.Create(model, new RequestContext("gem-session"));
        await StreamAsync(client, options: null, new ChatMessage(ChatRole.User, "Hi"));

        Assert.Equal(
            ["gem-session"],
            handler.Captures.Single().Request.Headers.GetValues("x-probe")
        );
    }

    [Fact]
    public async Task A_streamed_response_streams_text_and_use_over_the_gemini_sse()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            options: null,
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        Assert.Equal(
            "pong",
            Assert.Single(updates.SelectMany(update => update.Contents).OfType<TextContent>()).Text
        );
        UsageContent usage = Assert.Single(
            updates.SelectMany(update => update.Contents).OfType<UsageContent>()
        );
        Assert.Equal(5, usage.Details.InputTokenCount);
        Assert.Equal(1, usage.Details.OutputTokenCount);
        Assert.Contains(updates, update => update.ModelId == "gemini-2.5-flash");
    }

    [Fact]
    public async Task A_multi_chunk_stream_stops_when_the_finish_arrives()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.SplitTextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            options: null,
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        ChatMessage assistant = updates.ToChatResponse().Messages[0];
        Assert.Equal("pong", assistant.Text);
        Assert.Equal(ChatFinishReason.Stop, updates[^1].FinishReason);
    }

    [Fact]
    public async Task A_system_message_reaches_the_gemini_wire_as_system_instruction()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        await StreamAsync(
            client,
            options: null,
            new ChatMessage(ChatRole.System, "be brief"),
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        string body = handler.Captures.Single().Body;
        Assert.Contains("\"systemInstruction\"", body, StringComparison.Ordinal);
        Assert.Contains("be brief", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_user_image_reaches_the_gemini_wire_as_inline_data()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.TextTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        await StreamAsync(
            client,
            options: null,
            new ChatMessage(
                ChatRole.User,
                [
                    new TextContent("what is this image?"),
                    new DataContent(new byte[] { 1, 2, 3 }, "image/png"),
                ]
            )
        );

        string body = handler.Captures.Single().Body;
        Assert.Contains("\"inlineData\"", body, StringComparison.Ordinal);
        Assert.Contains("image/png", body, StringComparison.Ordinal);
        Assert.Contains(
            Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            body,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task A_streamed_tool_call_yields_a_function_call()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.ToolCallTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            options: GeminiToolOptions(),
            new ChatMessage(ChatRole.User, "Use the echo tool to echo the word ping.")
        );

        FunctionCallContent call = Assert.Single(
            updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>()
        );
        Assert.Equal("echo", call.Name);
        IDictionary<string, object?> arguments =
            call.Arguments ?? new Dictionary<string, object?>();
        var argument = Assert.Single(arguments);
        Assert.Equal("text", argument.Key);
        Assert.Equal("ping", argument.Value?.ToString());
        TextReasoningContent thinking = Assert.Single(
            updates.SelectMany(update => update.Contents).OfType<TextReasoningContent>()
        );
        Assert.Equal("weighing it", thinking.Text);
    }

    [Fact]
    public async Task Parallel_function_calls_arrive_as_two_calls_in_the_same_stream()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.ParallelToolCallTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            options: GeminiToolOptions(),
            new ChatMessage(ChatRole.User, "Echo ping and pong.")
        );

        List<FunctionCallContent> calls =
        [
            .. updates.SelectMany(update => update.Contents).OfType<FunctionCallContent>(),
        ];
        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal("echo", call.Name));
    }

    [Fact]
    public async Task A_streamed_thought_signature_surfaces_in_the_reasoning_content()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.ParallelToolCallTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            options: GeminiToolOptions(),
            new ChatMessage(ChatRole.User, "Echo ping and pong.")
        );

        ChatMessage assistant = updates.ToChatResponse().Messages[0];
        Assert.Contains(
            assistant.Contents.OfType<TextReasoningContent>(),
            reasoning => reasoning.ProtectedData == Signature
        );
    }

    [Fact]
    public async Task A_tool_result_flows_back_as_a_function_response_named_for_its_call()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(
            GoogleWirePayloads.ToolCallTurn,
            GoogleWirePayloads.FinalTurn
        );
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);
        ChatMessage prompt = new(ChatRole.User, "Use the echo tool to echo the word ping.");

        List<ChatResponseUpdate> first = await StreamAsync(
            client,
            options: GeminiToolOptions(),
            prompt
        );
        await StreamAsync(
            client,
            options: GeminiToolOptions(),
            prompt,
            first.ToChatResponse().Messages[0],
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", "pong")])
        );

        string body = handler.Captures[^1].Body;
        Assert.Contains("\"functionResponse\"", body, StringComparison.Ordinal);
        Assert.Contains("call_1", body, StringComparison.Ordinal);
        Assert.Contains("echo", body, StringComparison.Ordinal);
        Assert.Contains("pong", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_gemini_turn_after_a_signed_call_keeps_the_signature_on_the_wire()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(
            GoogleWirePayloads.ParallelToolCallTurn,
            GoogleWirePayloads.FinalTurn
        );
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);
        ChatMessage prompt = new(ChatRole.User, "Echo ping and pong.");

        List<ChatResponseUpdate> first = await StreamAsync(
            client,
            options: GeminiToolOptions(),
            prompt
        );
        await StreamAsync(
            client,
            options: GeminiToolOptions(),
            prompt,
            first.ToChatResponse().Messages[0],
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", "pong")])
        );

        // The signature the stream carried rides the next request: the SDK re-attaches the
        // thought signature from the returned reasoning content — a dropped one would make the
        // tool turn fail or degrade.
        Assert.Contains(Signature, handler.Captures[^1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cache_read_in_the_use_metadata_surfaces_as_cached_input_tokens()
    {
        using var environment = new EnvironmentScope((GoogleApiKeyVariable, "gem-probe-key"));
        var handler = new ScriptedGeminiHandler(GoogleWirePayloads.CachedTurn);
        ChatClientFactory factory = GeminiFactory(handler);
        IChatClient client = factory.Create(GeminiModel);

        List<ChatResponseUpdate> updates = await StreamAsync(
            client,
            options: null,
            new ChatMessage(ChatRole.User, "Reply with the single word: pong")
        );

        UsageContent usage = Assert.Single(
            updates.SelectMany(update => update.Contents).OfType<UsageContent>()
        );
        Assert.Equal(768, usage.Details.CachedInputTokenCount);
    }

    internal static readonly ModelInfo GeminiModel = new(
        "gemini-wire",
        "google",
        null,
        1_048_576,
        true
    )
    {
        ApiFamily = "google",
    };

    internal static ChatOptions GeminiToolOptions() => new() { Tools = [new EchoProbeTool()] };

    private static ChatClientFactory GeminiFactory(ScriptedGeminiHandler handler) =>
        new(new MarkingLoggerFactory(static () => { }), enableOpenTelemetry: false)
        {
            BaseTransportFactory = () => handler,
        };

    private static async Task<List<ChatResponseUpdate>> StreamAsync(
        IChatClient client,
        ChatOptions? options,
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

/// <summary>The scripted Gemini wire: serves each payload once in order and keeps every
/// request — captured at send time, because the SDK disposes the content afterwards.</summary>
internal sealed class ScriptedGeminiHandler(params string[] payloads) : HttpMessageHandler
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
        int index = _captures.Count;
        Assert.True(
            index < payloads.Length,
            $"The scripted Gemini wire was asked for response #{index}, but {payloads.Length} payloads were declared."
        );
        _captures.Add(new RequestCapture(request, body));
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(payloads[index]))),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return response;
    }
}
