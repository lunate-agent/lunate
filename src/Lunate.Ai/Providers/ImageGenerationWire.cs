using Microsoft.Extensions.AI;

namespace Lunate.Ai;

/// <summary>
/// One resolved image-generation wire: the model id the profile names for messages and attribution,
/// and the one call the provider's image API answers. The delegate hides MEAI's experimental image
/// types inside <see cref="ChatClientFactory"/>; consumers see <see cref="ImageGenerationWireResult"/>
/// and the stable <see cref="UsageDetails"/> only.
/// </summary>
internal sealed record ImageGenerationWire(
    string ModelId,
    Func<string, CancellationToken, Task<ImageGenerationWireResult>> Generate
);

/// <summary>The bytes one generation call returned, their media type and the provider's usage.</summary>
internal sealed record ImageGenerationWireResult(
    ReadOnlyMemory<byte> Data,
    string MediaType,
    UsageDetails? Usage
);
