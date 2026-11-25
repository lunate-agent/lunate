using System.Text.Json;
using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The optional <c>semantic_search</c> tool over a fake embeddings client: ranking, caps, argument
/// validation, the empty workspace and the provider failure. The fake's vectors count keyword
/// occurrences, so similarity is deterministic and no network is involved.
/// </summary>
public sealed class SemanticSearchToolTests
{
    private static readonly string[] Vocabulary = ["alpha", "beta"];

    [Fact]
    public void Tool_shape_is_pinned()
    {
        using var fixture = new Fixture();
        SemanticSearchTool tool = fixture.Tool(new KeywordEmbeddingsClient(Vocabulary));

        Assert.Equal("semantic_search", tool.Name);
        Assert.Equal(ToolRisk.ReadOnly, tool.Risk);
        JsonElement schema = tool.ParametersSchema;
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), element =>
            element.GetString() == "query"
        );
        Assert.True(schema.GetProperty("properties").TryGetProperty("max_results", out _));
    }

    [Fact]
    public async Task Hits_are_ranked_by_similarity_with_paths_lines_and_snippets()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), Lines(40, 3, "alpha alpha"));
        File.WriteAllText(fixture.Workspace.File("b.txt"), Lines(40, 11, "alpha"));
        SemanticSearchTool tool = fixture.Tool(new KeywordEmbeddingsClient(Vocabulary));

        ToolResult result = await RunAsync(tool, """{"query":"alpha"}""");

        Assert.False(result.IsError);
        string[] lines = result.Output.Split('\n');
        Assert.Equal("query: alpha", lines[0]);
        Assert.StartsWith("1. a.txt:1-40", lines[1], StringComparison.Ordinal);
        Assert.Contains("alpha alpha", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("2. b.txt:1-40", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_result_count_is_capped_with_a_marker()
    {
        using var fixture = new Fixture();
        for (var index = 0; index < 3; index++)
        {
            File.WriteAllText(fixture.Workspace.File($"f{index}.txt"), "alpha\n");
        }

        SemanticSearchTool tool = fixture.Tool(new KeywordEmbeddingsClient(Vocabulary));
        ToolResult result = await RunAsync(tool, """{"query":"alpha","max_results":2}""");

        Assert.False(result.IsError);
        string[] lines = result.Output.Split('\n');
        Assert.StartsWith("1. ", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("2. ", lines[2], StringComparison.Ordinal);
        Assert.Contains("more matching regions", lines[3], StringComparison.Ordinal);
        Assert.Equal(4, lines.Length);
    }

    [Theory]
    [InlineData("""{"query":""}""", "non-empty string")]
    [InlineData("""{"q":"alpha"}""", "accepted: query, max_results")]
    [InlineData("""{"query":"alpha","max_results":0}""", ">= 1")]
    public async Task Bad_arguments_are_refused_with_guidance(string arguments, string expected)
    {
        using var fixture = new Fixture();
        var client = new KeywordEmbeddingsClient(Vocabulary);
        SemanticSearchTool tool = fixture.Tool(client);

        ToolResult result = await RunAsync(tool, arguments);

        Assert.True(result.IsError);
        Assert.Contains(expected, result.Output, StringComparison.Ordinal);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task An_empty_workspace_is_a_result_not_an_error()
    {
        using var fixture = new Fixture();
        var client = new KeywordEmbeddingsClient(Vocabulary);
        SemanticSearchTool tool = fixture.Tool(client);

        ToolResult result = await RunAsync(tool, """{"query":"alpha"}""");

        Assert.False(result.IsError);
        Assert.Contains("No indexed files match", result.Output, StringComparison.Ordinal);
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task A_provider_failure_names_the_profile_and_the_model()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha\n");
        SemanticSearchTool tool = fixture.Tool(new ThrowingEmbeddingsClient());

        ToolResult result = await RunAsync(tool, """{"query":"alpha"}""");

        Assert.True(result.IsError);
        Assert.Contains("embedding-profile", result.Output, StringComparison.Ordinal);
        Assert.Contains("fake/embed", result.Output, StringComparison.Ordinal);
        Assert.Contains("grep", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_search_charges_its_usage_to_the_run_as_embeddings()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha\n");
        SemanticSearchTool tool = fixture.Tool(new KeywordEmbeddingsClient(Vocabulary));
        List<(UsageDetails Usage, string ModelId, ModelUsageAttribution Attribution)> recorded = [];

        ToolResult result = await RunAsync(
            tool,
            """{"query":"alpha"}""",
            new AgentToolContextState(null, null, null)
            {
                RecordUsage = (usage, modelId, attribution) =>
                    recorded.Add((usage, modelId, attribution)),
            }
        );

        Assert.False(result.IsError);
        Assert.Equal(2, recorded.Count);
        Assert.All(recorded, entry => Assert.Equal("embeddings", entry.Attribution.Usage));
        Assert.All(recorded, entry => Assert.Equal("fake/embed", entry.ModelId));
        Assert.All(recorded, entry =>
            Assert.Equal("embedding-profile", entry.Attribution.ProfileName)
        );
    }

    [Fact]
    public async Task The_workspace_gains_no_index_file()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha\n");
        SemanticSearchTool tool = fixture.Tool(new KeywordEmbeddingsClient(Vocabulary));

        await RunAsync(tool, """{"query":"alpha"}""");

        Assert.Equal(
            ["a.txt"],
            Directory
                .GetFiles(fixture.Workspace.Root, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Order()
        );
    }

    [Fact]
    public void The_default_cache_lives_under_the_state_root_not_the_workspace()
    {
        using var fixture = new Fixture();

        string cache = SemanticIndex.CacheFile(fixture.Workspace.Root, "fake/embed");

        Assert.Contains(Path.Combine("cache", "semantic-index"), cache, StringComparison.Ordinal);
        Assert.False(Workspace.IsInside(fixture.Workspace.Root, cache));
    }

    private static async Task<ToolResult> RunAsync(
        SemanticSearchTool tool,
        string arguments,
        object? agentState = null
    )
    {
        using JsonDocument document = JsonDocument.Parse(arguments);
        var context = new ToolContext("unused", new NullAgentEvents());
        if (agentState is not null)
        {
            context = context with { AgentState = agentState };
        }

        return await tool.ExecuteAsync(document.RootElement, context, CancellationToken.None);
    }

    private static string Lines(int count, int marked, string text) =>
        string.Join(
            '\n',
            Enumerable
                .Range(1, count)
                .Select(line => line == marked ? text : $"line {line} of filler text")
        );

    private sealed class Fixture : IDisposable
    {
        internal TempDirectory Workspace { get; } = new();

        internal TempDirectory Cache { get; } = new();

        internal SemanticSearchTool Tool(IEmbeddingsClient client) =>
            new(
                new Workspace(Workspace.Root),
                new EmbeddingsBinding(
                    client,
                    "embedding-profile",
                    "fake-provider",
                    "openai",
                    client.ModelId
                ),
                new SemanticIndex(
                    Workspace.Root,
                    Path.Combine(Cache.Root, "index.json"),
                    client.ModelId
                )
            );

        public void Dispose()
        {
            Cache.Dispose();
            Workspace.Dispose();
        }
    }

    private sealed class KeywordEmbeddingsClient : IEmbeddingsClient
    {
        private readonly string[] _vocabulary;

        public KeywordEmbeddingsClient(string[] vocabulary) => _vocabulary = vocabulary;

        public string ModelId => "fake/embed";

        public List<IReadOnlyList<string>> Requests { get; } = [];

        public Task<EmbeddingsResponse> EmbedAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(inputs);
            var usage = new UsageDetails { InputTokenCount = inputs.Count };
            return Task.FromResult(
                new EmbeddingsResponse(
                    [
                        .. inputs.Select(text =>
                            _vocabulary
                                .Select(word =>
                                    (float)
                                        text.Split(
                                                [' ', '\n'],
                                                StringSplitOptions.RemoveEmptyEntries
                                            )
                                            .Count(token =>
                                                token.Contains(
                                                    word,
                                                    StringComparison.OrdinalIgnoreCase
                                                )
                                            )
                                )
                                .ToArray()
                        ),
                    ],
                    usage
                )
            );
        }
    }

    private sealed class ThrowingEmbeddingsClient : IEmbeddingsClient
    {
        public string ModelId => "fake/embed";

        public Task<EmbeddingsResponse> EmbedAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("connection refused");
    }
}
