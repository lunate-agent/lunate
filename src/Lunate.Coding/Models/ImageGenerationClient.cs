using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The image-generation seam the <c>generate_image</c> tool talks to: one prompt in, the generated
/// bytes, their media type and the provider's usage out. The production client sits on the
/// provider's images wire; tests implement this directly so no network is involved.
/// </summary>
internal interface IImageGenerationClient
{
    /// <summary>The resolved model id the requests name, for messages and attribution.</summary>
    string ModelId { get; }

    /// <summary>Generates one image for <paramref name="prompt"/>.</summary>
    Task<GeneratedImageResult> GenerateAsync(string prompt, CancellationToken cancellationToken);
}

/// <summary>The bytes one generation call produced and the provider usage behind them.</summary>
internal sealed record GeneratedImageResult(
    ReadOnlyMemory<byte> Data,
    string MediaType,
    UsageDetails? Usage
);

/// <summary>
/// The provider-backed client: MEAI's image generator built over the same endpoint, headers and
/// credential placement the chat families use, so a provider's images wire needs no second
/// transport.
/// </summary>
internal sealed class ProviderImageGenerationClient(ImageGenerationWire wire)
    : IImageGenerationClient
{
    public string ModelId => wire.ModelId;

    public async Task<GeneratedImageResult> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ImageGenerationWireResult result = await wire.Generate(prompt, cancellationToken)
            .ConfigureAwait(false);
        return new GeneratedImageResult(result.Data, result.MediaType, result.Usage);
    }
}
