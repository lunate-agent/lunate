using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The embeddings seam the semantic search tool talks to: batched inputs in, one vector per input
/// out, plus the provider's usage report when it sends one. The production client sits on the
/// provider's <c>/embeddings</c> wire; tests implement this directly so no network is involved.
/// </summary>
internal interface IEmbeddingsClient
{
    /// <summary>The resolved model id the requests name, for messages and attribution.</summary>
    string ModelId { get; }

    /// <summary>Embeds every input, in order; the result has one vector per input.</summary>
    Task<EmbeddingsResponse> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken
    );
}

/// <summary>The vectors of one request and the provider usage that produced them.</summary>
internal sealed record EmbeddingsResponse(IReadOnlyList<float[]> Vectors, UsageDetails? Usage);

/// <summary>
/// The provider-backed client: MEAI's embedding generator built over the same endpoint, headers
/// and credential placement the chat families use, so a provider's embeddings wire needs no
/// second transport.
/// </summary>
internal sealed class ProviderEmbeddingsClient(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    string modelId
) : IEmbeddingsClient
{
    public string ModelId => modelId;

    public async Task<EmbeddingsResponse> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
        {
            return new EmbeddingsResponse([], Usage: null);
        }

        GeneratedEmbeddings<Embedding<float>> generated = await generator
            .GenerateAsync(inputs, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new EmbeddingsResponse(
            [.. generated.Select(embedding => embedding.Vector.ToArray())],
            generated.Usage
        );
    }
}
