namespace Lunate.Ai;

/// <summary>
/// The wire adapter stack a provider preset uses. <see cref="OpenAi"/> and <see cref="Anthropic"/>
/// are native adapters; <see cref="OpenAiCompatible"/> routes through the OpenAI-compatible adapter.
/// <see cref="OpenAiResponses"/> and <see cref="Google"/> exist for per-family routing that a
/// later slice of the provider-families change wires up.
/// </summary>
public enum ProviderAdapter
{
    OpenAi,
    Anthropic,
    OpenAiCompatible,
    OpenAiResponses,
    Google,
}
