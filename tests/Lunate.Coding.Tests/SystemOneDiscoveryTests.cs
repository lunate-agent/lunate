using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

public sealed class SystemOneDiscoveryTests
{
    [Fact]
    public async Task OpenRouter_discovery_marks_systemone_from_endpoint_metadata()
    {
        using var temp = new TempDirectory();
        WriteCatalog(temp);
        using var handler = new FakeHandler(
            """{"data":[{"id":"typesafe/jev-1.14","api":{"systemone":{}}}]}"""
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "openrouter",
            ModelCatalog.Load(temp.File("models.json")),
            handler,
            environment: _ => "test-key",
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://openrouter.ai/api/v1/models",
            handler.Request.RequestUri!.AbsoluteUri
        );
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement model = Assert.Single(draft.RootElement.GetProperty("models").EnumerateArray());
        Assert.Equal("openrouter/typesafe/jev-1.14", model.GetProperty("id").GetString());
        Assert.Equal("systemone", model.GetProperty("apiFamily").GetString());
        Assert.Equal("native", model.GetProperty("apiType").GetString());
    }

    [Fact]
    public async Task OpenCode_discovery_falls_back_to_the_catalog_order_sensitivity()
    {
        using var temp = new TempDirectory();
        WriteCatalog(temp);
        using var handler = new FakeHandler("""{"data":[{"id":"jev-1.13"}]}""");

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "opencode",
            ModelCatalog.Load(temp.File("models.json")),
            handler,
            environment: _ => "test-key",
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://opencode.ai/inference/openai/v1/models",
            handler.Request.RequestUri!.AbsoluteUri
        );
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement model = Assert.Single(draft.RootElement.GetProperty("models").EnumerateArray());
        Assert.Equal("opencode/jev-1.13", model.GetProperty("id").GetString());
        Assert.Equal("systemone", model.GetProperty("apiFamily").GetString());
        Assert.Equal("native", model.GetProperty("apiType").GetString());
        Assert.True(model.GetProperty("choiceOrderSensitive").GetBoolean());
    }

    private static void WriteCatalog(TempDirectory temp) =>
        File.WriteAllText(
            temp.File("models.json"),
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "openrouter/typesafe/jev-1.14", "provider": "openrouter",
                  "wireId": "typesafe/jev-1.14", "contextWindow": 65536,
                  "supportsTools": false, "supportsVision": true,
                  "apiType": "native", "apiFamily": "systemone" },
                { "id": "opencode/jev-1.13", "provider": "opencode",
                  "wireId": "jev-1.13", "contextWindow": 65536,
                  "supportsTools": false, "apiType": "native",
                  "apiFamily": "systemone", "choiceOrderSensitive": true }
              ]
            }
            """
        );

    private sealed class FakeHandler(string response) : HttpMessageHandler
    {
        public HttpRequestMessage Request { get; private set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json"),
                }
            );
        }
    }
}
