using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The embeddings usage end to end through an interactive session: an enabled
/// <c>semantic_search</c> tool embeds through the provider's wire, the call is charged to the run
/// as usage <c>embeddings</c>, and the session records it without a schema change so <c>/cost</c>
/// groups it under its own usage.
/// </summary>
public sealed class SemanticSearchSessionTests
{
    [Fact]
    public async Task A_search_records_an_embeddings_usage_the_cost_report_shows()
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
                { "id": "embedding-model", "provider": "openai", "contextWindow": 8192,
                  "supportsTools": false, "pricing": { "input": 0.02, "output": 0 } }
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
            BaseTransportFactory = () => new EmbeddingsTransport(),
        };
        using var host = new InteractiveSessionHost(
            factory: factory,
            modelsPath: modelsPath,
            settingsJson: """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-profile": { "provider": "chat-provider", "model": "chat-model" },
                "embedding-profile": {
                  "provider": "chat-provider",
                  "model": "embedding-model"
                }
              },
              "usages": { "chat": "chat-profile", "embeddings": "embedding-profile" },
              "tools": { "extra": ["semantic_search"] }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("chat-provider")]
        );
        // The session's workspace is the host's own temp root, not this test's.
        File.WriteAllText(host.Temp.File("workspace-note.txt"), "alpha goes here\n");
        chat = host.Client;
        try
        {
            await DriveSessionAsync(host);
        }
        finally
        {
            // The default index cache is keyed by the workspace's canonical root (the tool's
            // Workspace resolves symlinks), which is what this deletes.
            string cache = SemanticIndex.CacheFile(
                new Workspace(host.Temp.Root).WorktreeRoot,
                "embedding-model"
            );
            if (File.Exists(cache))
            {
                File.Delete(cache);
            }
        }
    }

    private static async Task DriveSessionAsync(InteractiveSessionHost host)
    {
        host.Client.Enqueue(
            Scripts.Call("call-1", "semantic_search", Scripts.Args(("query", "alpha"))),
            Scripts.ToolCalls()
        );
        host.Client.Enqueue(Scripts.Text("done"), Scripts.Stop());
        Task run = host.RunAsync();
        host.Scrollback.Profile.Width = 400;
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("please search");
        host.Console.SendEnter();
        await host.Client.WaitForCallAsync(2);

        host.Console.SendText("/cost");
        host.Console.SendEnter();
        string report = await InteractiveSessionCommandTests.WaitForScrollbackAsync(
            host,
            "cost by usage:"
        );

        Assert.Contains(host.Client.RequestOptions[0]!.Tools!, tool =>
            tool.Name == "semantic_search"
        );
        Assert.Contains("embeddings", report, StringComparison.Ordinal);
        Session session = Session.Load(
            Assert.Single(Directory.GetFiles(host.Temp.File("sessions"), "*.jsonl"))
        );
        SessionModelUsageEntry usage = Assert.Single(
            session.Entries.OfType<SessionModelUsageEntry>(),
            entry => entry.Attribution?.Usage == "embeddings"
        );
        Assert.Equal("embedding-profile", usage.Attribution!.ProfileName);
        Assert.Equal("embedding-model", usage.Model);
        Assert.Equal(4, session.Header.Schema);

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

    /// <summary>The provider's embeddings wire: one vector per one-input request, with usage.</summary>
    private sealed class EmbeddingsTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                            "object": "list",
                            "data": [
                                {"object": "embedding", "index": 0, "embedding": [0.1, 0.2]}
                            ],
                            "model": "embedding-model",
                            "usage": {"prompt_tokens": 2, "total_tokens": 2}
                        }
                        """,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
    }
}
