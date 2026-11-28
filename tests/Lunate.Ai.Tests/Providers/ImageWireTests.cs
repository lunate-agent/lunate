using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Ai.Tests;

/// <summary>
/// The image-generation seam on the provider factory: the request goes to the provider's
/// <c>/images/generations</c> path with the declared credential placement and wire model id, and the
/// response comes back as MEAI image content with the provider's usage.
/// </summary>
public sealed class ImageWireTests
{
    [Fact]
    public async Task The_request_reaches_the_images_path_with_the_credential_and_wire_id()
    {
        var capture = new CapturingTransport();
        var factory = new ChatClientFactory(NullLoggerFactory.Instance, enableOpenTelemetry: false)
        {
            BaseTransportFactory = () => capture,
        };
        ModelInfo model = new(
            "openai/gpt-image-1",
            "openai",
            Endpoint: null,
            ContextWindow: 0,
            SupportsTools: false
        )
        {
            WireId = "gpt-image-1",
        };

        ImageGenerationWire wire = factory.CreateImageGenerationWire(
            model,
            apiKey: "sk-test",
            context: null
        );
        ImageGenerationWireResult response = await wire.Generate(
            "a red square",
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpMethod.Post, capture.Request!.Method);
        Assert.EndsWith(
            "/images/generations",
            capture.Request.RequestUri!.AbsolutePath,
            StringComparison.Ordinal
        );
        Assert.Equal("Bearer sk-test", capture.Request.Headers.GetValues("Authorization").Single());
        Assert.Contains("\"prompt\":\"a red square\"", capture.Body, StringComparison.Ordinal);
        Assert.Contains("\"model\":\"gpt-image-1\"", capture.Body, StringComparison.Ordinal);
        Assert.Equal("image/png", response.MediaType);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], response.Data.ToArray());
        Assert.Equal(10, response.Usage?.InputTokenCount);
        Assert.Equal(20, response.Usage?.OutputTokenCount);
    }

    [Fact]
    public void A_family_without_an_image_wire_is_refused_by_name()
    {
        var factory = new ChatClientFactory(NullLoggerFactory.Instance, enableOpenTelemetry: false);
        ModelInfo model = new(
            "anthropic/claude-image",
            "anthropic",
            Endpoint: null,
            ContextWindow: 4096,
            SupportsTools: false
        )
        {
            ApiFamily = ProviderApiFamilies.AnthropicMessages,
        };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            factory.CreateImageGenerationWire(model, apiKey: "sk-test", context: null)
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
                        "created": 1700000000,
                        "output_format": "png",
                        "data": [
                            {"b64_json": "iVBORw=="}
                        ],
                        "usage": {
                            "input_tokens": 10,
                            "output_tokens": 20,
                            "total_tokens": 30
                        }
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
