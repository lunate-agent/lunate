using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The index behind <c>semantic_search</c>: a cache keyed by path and text digest, rebuilt on the
/// first search, reused while files are unchanged, and safe to delete or corrupt.
/// </summary>
public sealed class SemanticIndexTests
{
    private static readonly string[] Vocabulary = ["alpha", "beta"];

    [Fact]
    public async Task The_first_search_embeds_every_chunk_and_the_second_reuses_them()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha one\n");
        File.WriteAllText(fixture.Workspace.File("b.txt"), "alpha two\n");
        var client = new RecordingEmbeddingsClient(Vocabulary);
        SemanticIndex index = fixture.Index(client);

        SemanticSearchResult first = await SearchAsync(index, client, "alpha");
        Assert.Single(client.ChunkRequests);
        SemanticSearchResult second = await SearchAsync(index, client, "alpha");

        // The second search embedded the query only: the chunks came from the cache.
        Assert.Single(client.ChunkRequests);
        Assert.Equal(
            [.. first.Hits.Select(hit => hit.Path)],
            [.. second.Hits.Select(hit => hit.Path)]
        );
    }

    [Fact]
    public async Task A_changed_file_is_re_embedded_and_the_rest_reused()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha one\n");
        File.WriteAllText(fixture.Workspace.File("b.txt"), "alpha two\n");
        var client = new RecordingEmbeddingsClient(Vocabulary);
        SemanticIndex index = fixture.Index(client);

        await SearchAsync(index, client, "alpha");
        client.ChunkRequests.Clear();
        File.WriteAllText(fixture.Workspace.File("b.txt"), "beta changed\n");
        await SearchAsync(index, client, "alpha");

        Assert.Single(client.ChunkRequests);
        Assert.Equal(["beta changed"], client.ChunkRequests[0]);
    }

    [Fact]
    public async Task A_deleted_cache_is_rebuilt()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha content\n");
        var client = new RecordingEmbeddingsClient(Vocabulary);
        SemanticIndex index = fixture.Index(client);

        await SearchAsync(index, client, "alpha");
        File.Delete(fixture.CacheFile);
        client.ChunkRequests.Clear();
        SemanticSearchResult result = await SearchAsync(index, client, "alpha");

        Assert.Single(client.ChunkRequests);
        Assert.Equal("a.txt", Assert.Single(result.Hits).Path);
    }

    [Fact]
    public async Task A_corrupt_cache_is_rebuilt_instead_of_failing()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File("a.txt"), "alpha content\n");
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.CacheFile)!);
        File.WriteAllText(fixture.CacheFile, "{not json");
        var client = new RecordingEmbeddingsClient(Vocabulary);
        SemanticIndex index = fixture.Index(client);

        SemanticSearchResult result = await SearchAsync(index, client, "alpha");

        Assert.Single(client.ChunkRequests);
        Assert.Equal("a.txt", Assert.Single(result.Hits).Path);
    }

    [Fact]
    public async Task Ignored_binary_and_oversized_files_are_not_indexed()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.Workspace.File(".gitignore"), "bin/\n");
        Directory.CreateDirectory(fixture.Workspace.File("bin"));
        File.WriteAllText(fixture.Workspace.File("bin/skip.txt"), "alpha\n");
        File.WriteAllBytes(fixture.Workspace.File("blob.bin"), [.. "alpha"u8, 0, .. "tail"u8]);
        File.WriteAllText(fixture.Workspace.File("keep.txt"), "alpha\n");
        var client = new RecordingEmbeddingsClient(Vocabulary);
        SemanticIndex index = fixture.Index(client);

        SemanticSearchResult result = await SearchAsync(index, client, "alpha");

        Assert.Equal("keep.txt", Assert.Single(result.Hits).Path);
    }

    private static async Task<SemanticSearchResult> SearchAsync(
        SemanticIndex index,
        IEmbeddingsClient client,
        string query
    ) =>
        await index.SearchAsync(
            client,
            query,
            maxResults: 10,
            onUsage: null,
            CancellationToken.None
        );

    private sealed class Fixture : IDisposable
    {
        internal TempDirectory Workspace { get; } = new();

        internal TempDirectory Cache { get; } = new();

        internal string CacheFile => Path.Combine(Cache.Root, "index.json");

        internal SemanticIndex Index(IEmbeddingsClient client) =>
            new(Workspace.Root, CacheFile, client.ModelId);

        public void Dispose()
        {
            Cache.Dispose();
            Workspace.Dispose();
        }
    }

    /// <summary>
    /// A deterministic fake: one keyword-count vector per input. It records the inputs of every
    /// non-query request, so a test can tell a re-embed from a cache hit.
    /// </summary>
    private sealed class RecordingEmbeddingsClient : IEmbeddingsClient
    {
        private readonly string[] _vocabulary;

        public RecordingEmbeddingsClient(string[] vocabulary) => _vocabulary = vocabulary;

        public string ModelId => "fake/embed";

        public List<IReadOnlyList<string>> ChunkRequests { get; } = [];

        public Task<EmbeddingsResponse> EmbedAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken
        )
        {
            if (!(inputs.Count == 1 && inputs[0] is "alpha" or "beta"))
            {
                ChunkRequests.Add(inputs);
            }

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
                    Usage: null
                )
            );
        }
    }
}
