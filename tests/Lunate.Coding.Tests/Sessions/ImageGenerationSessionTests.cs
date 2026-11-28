using System.Text;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The image usage end to end through an interactive session: the <c>generate_image</c> tool asks the
/// provider's images wire, the call is charged to the run as usage <c>image</c>, the artefact lands in
/// the session's sidecar and the transcript, and the session records the usage without a schema
/// change so <c>/cost</c> groups it under its own usage.
/// </summary>
public sealed class ImageGenerationSessionTests
{
    [Fact]
    public async Task A_generation_records_an_image_usage_the_cost_report_shows()
    {
        using var temp = new TempDirectory();
        string modelsPath = temp.File("models.json");
        File.WriteAllText(
            modelsPath,
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "chat-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": true },
                { "id": "gpt-image-1", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": false, "pricing": { "input": 5, "output": 30 } }
              ]
            }
            """
        );

        GatedChatClient chat = null!;
        var factory = new ChatClientFactory(
            NullLoggerFactory.Instance,
            enableOpenTelemetry: false,
            providerClientFactory: (_, _) => chat,
            namedKeySource: _ => "sk-test"
        )
        {
            BaseTransportFactory = () => new ImagesTransport(),
        };
        using var host = new InteractiveSessionHost(
            factory: factory,
            modelsPath: modelsPath,
            settingsJson: """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-profile": { "provider": "chat-provider", "model": "chat-model" },
                "image-profile": { "provider": "chat-provider", "model": "gpt-image-1" }
              },
              "usages": { "chat": "chat-profile", "image": "image-profile" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("chat-provider")]
        );
        chat = host.Client;
        host.Client.Enqueue(
            Scripts.Call("call-1", "generate_image", Scripts.Args(("prompt", "a red square"))),
            Scripts.ToolCalls()
        );
        host.Client.Enqueue(Scripts.Text("done"), Scripts.Stop());
        Task run = host.RunAsync();
        host.Scrollback.Profile.Width = 400;
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("draw a red square");
        host.Console.SendEnter();
        // generate_image carries the write risk, so the interactive session asks first.
        await host.WaitForApprovalAsync();
        host.Console.SendText("y");
        await host.Client.WaitForCallAsync(2);
        await host.WaitUntilAsync(() => !host.Session.IsRunning);

        host.Console.SendText("/cost");
        host.Console.SendEnter();
        string report = await InteractiveSessionCommandTests.WaitForScrollbackAsync(
            host,
            "cost by usage:"
        );

        Assert.Contains(host.Client.RequestOptions[0]!.Tools!, tool =>
            tool.Name == "generate_image"
        );
        Assert.Contains("image", report, StringComparison.Ordinal);

        Session session = Session.Load(
            Assert.Single(Directory.GetFiles(host.Temp.File("sessions"), "*.jsonl"))
        );
        SessionModelUsageEntry usage = Assert.Single(
            session.Entries.OfType<SessionModelUsageEntry>(),
            entry => entry.Attribution?.Usage == "image"
        );
        Assert.Equal("image-profile", usage.Attribution!.ProfileName);
        Assert.Equal("gpt-image-1", usage.Model);
        Assert.Equal(4, session.Header.Schema);

        ImageContentReference reference = Assert.Single(
            session
                .Entries.OfType<SessionMessageEntry>()
                .SelectMany(entry => entry.Message.Contents)
                .OfType<ImageContentReference>()
        );
        Assert.Equal("image/png", reference.MimeType);
        string blob = Path.Combine(
            Path.GetDirectoryName(session.Path)!,
            session.SessionId,
            "images",
            reference.Digest + ".png"
        );
        Assert.True(File.Exists(blob), $"expected the sidecar blob at {blob}");
        Assert.Equal(ImageSamples.Png(2, 3), File.ReadAllBytes(blob));

        host.Console.Complete();
        await run;
    }

    private static ProviderEntry Provider(string name)
    {
        bool valid = ProviderEntry.TryCreate(
            name,
            "openai",
            new Uri("https://provider.example.test/v1"),
            $"auth:{name}",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );

        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }

    /// <summary>The provider's images wire: one base64 PNG and the provider's usage.</summary>
    private sealed class ImagesTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""
                        {
                            "created": 1700000000,
                            "output_format": "png",
                            "data": [ { "b64_json": "{{Convert.ToBase64String(
                            ImageSamples.Png(2, 3)
                        )}}" } ],
                            "usage": { "input_tokens": 10, "output_tokens": 20, "total_tokens": 30 }
                        }
                        """,
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
    }
}
