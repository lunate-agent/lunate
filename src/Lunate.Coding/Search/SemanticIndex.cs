using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>One indexed region, as the search tool renders it.</summary>
internal sealed record SemanticHit(
    string Path,
    int StartLine,
    int EndLine,
    string Snippet,
    double Score
);

/// <summary>The ranked hits of one search, the display cap and whether the index cap was reached.</summary>
internal sealed record SemanticSearchResult(
    IReadOnlyList<SemanticHit> Hits,
    bool Truncated,
    bool IndexCapped
);

/// <summary>
/// The workspace index behind <c>semantic_search</c>: line chunks and their embeddings, kept as a
/// rebuildable cache under the Lunate state root's <c>cache</c> directory — never in the workspace,
/// never in a session. A chunk is reused while its path and text digest are unchanged, so an edit
/// re-embeds one file and deleting the cache costs only the re-embedding. The walk is the search
/// tools' walk: the <c>.gitignore</c> chain, no <c>.git</c>, no directory symlink, binary files and
/// files above 256 KiB skipped.
/// </summary>
internal sealed class SemanticIndex(string root, string cacheFile, string modelId)
{
    internal const int ChunkLines = 40;
    internal const int MaxChunks = 2_000;
    internal const int MaxFileBytes = 256 * 1024;
    internal const int BatchSize = 64;
    private const int ProbeBytes = 8192;
    private const int CacheVersion = 1;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _root = Path.GetFullPath(root);
    private readonly string _cacheFile = cacheFile;
    private readonly string _modelId = modelId;

    /// <summary>The cache file for a workspace and resolved embeddings model.</summary>
    internal static string CacheFile(string workspaceRoot, string modelId) =>
        Path.Combine(
            Path.Combine(LunateHome.Store, "cache", "semantic-index"),
            string.Concat(Hash8(Path.GetFullPath(workspaceRoot)), "-", Hash8(modelId), ".json")
        );

    /// <summary>
    /// Builds or refreshes the index and ranks the workspace regions against the query. Every
    /// embeddings request reports its usage through <paramref name="onUsage"/> so the run charges
    /// it like any other model call.
    /// </summary>
    internal async Task<SemanticSearchResult> SearchAsync(
        IEmbeddingsClient client,
        string query,
        int maxResults,
        Action<UsageDetails>? onUsage,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        (List<IndexChunk> chunks, bool indexCapped) = await EnsureAsync(
                client,
                onUsage,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (chunks.Count == 0)
        {
            return new SemanticSearchResult([], Truncated: false, indexCapped);
        }

        EmbeddingsResponse response = await client
            .EmbedAsync([query], cancellationToken)
            .ConfigureAwait(false);
        if (response.Usage is { } usage)
        {
            onUsage?.Invoke(usage);
        }

        if (response.Vectors is not [float[] queryVector, ..])
        {
            throw new InvalidDataException(
                $"the embeddings model '{_modelId}' returned no vector for the query"
            );
        }

        List<SemanticHit> hits = [];
        foreach (IndexChunk chunk in chunks)
        {
            double score = Cosine(queryVector, chunk.Vector);
            if (double.IsNaN(score))
            {
                continue;
            }

            hits.Add(
                new SemanticHit(
                    chunk.Path,
                    chunk.StartLine,
                    chunk.EndLine,
                    BestSnippet(chunk.Text, query),
                    score
                )
            );
        }

        hits.Sort(
            static (left, right) =>
            {
                int byScore = right.Score.CompareTo(left.Score);
                if (byScore != 0)
                {
                    return byScore;
                }

                int byPath = string.CompareOrdinal(left.Path, right.Path);
                return byPath != 0 ? byPath : left.StartLine.CompareTo(right.StartLine);
            }
        );
        bool truncated = hits.Count > maxResults;
        if (truncated)
        {
            hits.RemoveRange(maxResults, hits.Count - maxResults);
        }

        return new SemanticSearchResult(hits, truncated, indexCapped);
    }

    private async Task<(List<IndexChunk> Chunks, bool Capped)> EnsureAsync(
        IEmbeddingsClient client,
        Action<UsageDetails>? onUsage,
        CancellationToken cancellationToken
    )
    {
        (List<ChunkSource> sources, bool capped) = CollectSources(cancellationToken);
        Dictionary<(string Path, string Digest), float[]> cached = LoadCache();

        var chunks = new List<IndexChunk>(sources.Count);
        var pending = new List<IndexChunk>();
        foreach (ChunkSource source in sources)
        {
            if (cached.TryGetValue((source.Path, source.Digest), out float[]? vector))
            {
                chunks.Add(new IndexChunk(source, vector));
                continue;
            }

            var chunk = new IndexChunk(source, []);
            chunks.Add(chunk);
            pending.Add(chunk);
        }

        for (var offset = 0; offset < pending.Count; offset += BatchSize)
        {
            List<IndexChunk> batch = pending.GetRange(
                offset,
                Math.Min(BatchSize, pending.Count - offset)
            );
            EmbeddingsResponse response = await client
                .EmbedAsync([.. batch.Select(chunk => chunk.Text)], cancellationToken)
                .ConfigureAwait(false);
            if (response.Usage is { } usage)
            {
                onUsage?.Invoke(usage);
            }

            if (response.Vectors.Count != batch.Count)
            {
                throw new InvalidDataException(
                    $"the embeddings model '{_modelId}' returned {response.Vectors.Count.ToString(CultureInfo.InvariantCulture)} vectors for "
                        + $"{batch.Count.ToString(CultureInfo.InvariantCulture)} inputs"
                );
            }

            for (var index = 0; index < batch.Count; index++)
            {
                batch[index].Vector = response.Vectors[index];
            }
        }

        if (pending.Count > 0 || !File.Exists(_cacheFile))
        {
            SaveCache(chunks);
        }

        return (chunks, capped);
    }

    /// <summary>
    /// The workspace's chunks in deterministic path order: the search tools' walk, a binary probe,
    /// the size cap and the chunk cap. Reading stops at the cap, so a huge workspace pays for the
    /// capped prefix only.
    /// </summary>
    internal (List<ChunkSource> Sources, bool Capped) CollectSources(CancellationToken ct)
    {
        List<ChunkSource> sources = [];
        var capped = false;
        IReadOnlyList<WorkspaceEntry> entries;
        try
        {
            entries = FileIndex.VisibleEntries(new SystemWorkspaceFiles(_root));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (sources, capped);
        }

        foreach (WorkspaceEntry entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                continue;
            }

            if (sources.Count >= MaxChunks)
            {
                capped = true;
                break;
            }

            string absolute = Path.Combine(
                _root,
                entry.Path.Replace('/', Path.DirectorySeparatorChar)
            );
            string? text = ReadText(absolute);
            if (text is null)
            {
                continue;
            }

            sources.AddRange(ChunksOf(entry.Path, text, MaxChunks - sources.Count));
        }

        return (sources, capped || sources.Count >= MaxChunks);
    }

    private static IEnumerable<ChunkSource> ChunksOf(string path, string text, int room)
    {
        string[] lines = TextFile.SplitLines(text);
        var taken = 0;
        for (var start = 0; start < lines.Length && taken < room; start += ChunkLines)
        {
            int end = Math.Min(start + ChunkLines, lines.Length);
            string chunkText = string.Join('\n', lines[start..end]);
            if (string.IsNullOrWhiteSpace(chunkText))
            {
                continue;
            }

            taken++;
            yield return new ChunkSource(path, start + 1, end, chunkText, Digest(chunkText));
        }
    }

    /// <summary>
    /// The chunk line that best names the query: the line matching the most query words, ties going
    /// to the earlier line, falling back to the first non-blank line. The model reads this as the
    /// hit's excerpt, so it should be the line the query is about rather than the chunk's first.
    /// </summary>
    internal static string BestSnippet(string chunkText, string query)
    {
        string[] terms =
        [
            .. query.Split(
                [' ', '\t', '\n', '.', ',', ':', ';', '(', ')', '[', ']', '{', '}', '"', '\''],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            ),
        ];
        string? best = null;
        var bestMatches = 0;
        foreach (string line in TextFile.SplitLines(chunkText))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            int matches = terms.Count(term =>
                trimmed.Contains(term, StringComparison.OrdinalIgnoreCase)
            );
            if (best is null || matches > bestMatches)
            {
                best = trimmed;
                bestMatches = matches;
            }

            if (matches == terms.Length)
            {
                break;
            }
        }

        string snippet = best ?? string.Empty;
        return snippet.Length <= 160 ? snippet : Truncate(snippet, 160) + "…";
    }

    /// <summary>Cuts at a UTF-16 boundary, never between a surrogate pair.</summary>
    private static string Truncate(string value, int length) =>
        char.IsHighSurrogate(value[length - 1]) ? value[..(length - 1)] : value[..length];

    private static string? ReadText(string absolute)
    {
        try
        {
            var info = new FileInfo(absolute);
            if (!info.Exists || info.Length == 0 || info.Length > MaxFileBytes)
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(absolute);
            int probe = Math.Min(bytes.Length, ProbeBytes);
            if (Array.IndexOf(bytes, (byte)0, 0, probe) >= 0)
            {
                return null;
            }

            return Utf8.GetString(bytes);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private Dictionary<(string Path, string Digest), float[]> LoadCache()
    {
        var entries = new Dictionary<(string Path, string Digest), float[]>();
        try
        {
            if (!File.Exists(_cacheFile))
            {
                return entries;
            }

            CacheDocument? document = JsonSerializer.Deserialize<CacheDocument>(
                File.ReadAllText(_cacheFile, Utf8)
            );
            if (
                document is not { Version: CacheVersion }
                || !string.Equals(document.Model, _modelId, StringComparison.Ordinal)
                || document.Chunks is null
            )
            {
                return entries;
            }

            foreach (CacheChunk chunk in document.Chunks)
            {
                if (
                    chunk.Path is { Length: > 0 }
                    && chunk.Digest is { Length: > 0 }
                    && chunk.Vector is { Length: > 0 }
                )
                {
                    entries[(chunk.Path, chunk.Digest)] = chunk.Vector;
                }
            }
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or JsonException
                        or NotSupportedException
            )
        {
            // A corrupt or unreadable cache is the same as no cache: rebuild it.
            entries.Clear();
        }

        return entries;
    }

    private void SaveCache(IReadOnlyList<IndexChunk> chunks)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_cacheFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new CacheDocument(
                CacheVersion,
                _modelId,
                [.. chunks.Select(chunk => new CacheChunk(chunk.Path, chunk.Digest, chunk.Vector))]
            );
            string temporary = _cacheFile + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(document), Utf8);
            File.Move(temporary, _cacheFile, overwrite: true);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // The cache is an optimisation: a failed write keeps the previous file usable and the
            // in-memory index serves this call.
        }
    }

    private static double Cosine(float[] left, float[] right)
    {
        if (left.Length != right.Length || left.Length == 0)
        {
            return double.NaN;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        return leftNorm == 0 || rightNorm == 0
            ? double.NaN
            : dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }

    private static string Digest(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(text)));

    private static string Hash8(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(value)))[..8];

    /// <summary>One chunk as read from the workspace, before its vector is known.</summary>
    internal sealed record ChunkSource(
        string Path,
        int StartLine,
        int EndLine,
        string Text,
        string Digest
    );

    private sealed class IndexChunk(ChunkSource source, float[] vector)
    {
        internal string Path => source.Path;
        internal int StartLine => source.StartLine;
        internal int EndLine => source.EndLine;
        internal string Text => source.Text;
        internal string Digest => source.Digest;
        internal float[] Vector { get; set; } = vector;
    }

    private sealed record CacheDocument(int Version, string Model, List<CacheChunk>? Chunks);

    private sealed record CacheChunk(string Path, string Digest, float[]? Vector);
}
