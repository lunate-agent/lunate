namespace Lunate.Ai;

/// <summary>
/// The wire adapter stack a provider preset uses. <see cref="OpenAi"/> and <see cref="Anthropic"/>
/// are native adapters; <see cref="OpenAiCompatible"/> routes through the OpenAI-compatible adapter.
/// <see cref="ProviderApiFamily"/> selects the client a model's family needs; no preset declares
/// <see cref="OpenAiResponses"/> or <see cref="Google"/> as its adapter kind.
/// </summary>
public enum ProviderAdapter
{
    OpenAi,
    Anthropic,
    OpenAiCompatible,
    OpenAiResponses,
    Google,
}
