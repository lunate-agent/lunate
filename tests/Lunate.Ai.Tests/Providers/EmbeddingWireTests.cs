using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Ai.Tests;

/// <summary>
/// The embeddings seam on the provider factory: the request goes to the provider's
/// <c>/embeddings</c> path with the declared credential placement and wire model id, and the
/// response comes back as MEAI embeddings with the provider's usage.
/// </summary>
public sealed class EmbeddingWireTests
{
    [Fact]
    public async Task The_request_reaches_the_embeddings_path_with_the_credential_and_wire_id()
    {
        var capture = new CapturingTransport();
        var factory = new ChatClientFactory(NullLoggerFactory.Instance, enableOpenTelemetry: false)
        {
            BaseTransportFactory = () => capture,
        };
        ModelInfo model = new(
            "openai/text-embedding-3-small",
            "openai",
            Endpoint: null,
            ContextWindow: 8191,
            SupportsTools: false
        )
        {
            WireId = "text-embedding-3-small",
        };

        IEmbeddingGenerator<string, Embedding<float>> generator = factory.CreateEmbeddingGenerator(
            model,
            apiKey: "sk-test",
            context: null
        );
        GeneratedEmbeddings<Embedding<float>> generated = await generator.GenerateAsync(
            ["hello", "world"],
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpMethod.Post, capture.Request!.Method);
        Assert.EndsWith(
            "/embeddings",
            capture.Request.RequestUri!.AbsolutePath,
            StringComparison.Ordinal
        );
        Assert.Equal("Bearer sk-test", capture.Request.Headers.GetValues("Authorization").Single());
        Assert.Contains(
            "\"model\":\"text-embedding-3-small\"",
            capture.Body,
            StringComparison.Ordinal
        );
        Assert.Contains("\"input\":[\"hello\",\"world\"]", capture.Body, StringComparison.Ordinal);
        Assert.Equal(2, generated.Count);
        Assert.Equal([0.1f, 0.2f], generated[0].Vector.ToArray());
        Assert.Equal(2, generated.Usage?.InputTokenCount);
    }

    [Fact]
    public void A_family_without_an_embeddings_wire_is_refused_by_name()
    {
        var factory = new ChatClientFactory(NullLoggerFactory.Instance, enableOpenTelemetry: false);
        ModelInfo model = new(
            "anthropic/claude-embed",
            "anthropic",
            Endpoint: null,
            ContextWindow: 4096,
            SupportsTools: false
        )
        {
            ApiFamily = ProviderApiFamilies.AnthropicMessages,
        };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            factory.CreateEmbeddingGenerator(model, apiKey: "sk-test", context: null)
        );

        Assert.Contains(
            ProviderApiFamilies.AnthropicMessages,
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            ProviderApiFamilies.OpenAiChat,
            exception.Message,
            StringComparison.Ordinal
        );
    }

    private sealed class CapturingTransport : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                        "object": "list",
                        "data": [
                            {"object": "embedding", "index": 0, "embedding": [0.1, 0.2]},
                            {"object": "embedding", "index": 1, "embedding": [0.3, 0.4]}
                        ],
                        "model": "text-embedding-3-small",
                        "usage": {"prompt_tokens": 2, "total_tokens": 2}
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
