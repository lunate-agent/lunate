using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding;

internal sealed record DiscoveryModelMetadata(
    string ApiFamily,
    Uri? Endpoint,
    int? ContextWindow,
    int? MaxOutput,
    bool? SupportsTools,
    bool? SupportsVision,
    string? Reasoning,
    bool? SupportsTemperature,
    ModelCapabilityDescriptor? Capabilities
);

/// <summary>Maps the per-model metadata returned by OpenCode Go's OpenAI-compatible listing.</summary>
internal static class DiscoveryModelMetadataReader
{
    public static bool TryRead(
        JsonElement model,
        string configuredBaseUrl,
        bool pinToConfiguredHost,
        out DiscoveryModelMetadata? metadata,
        out string? error
    )
    {
        metadata = null;
        error = null;
        if (!model.TryGetProperty("api", out JsonElement apis))
        {
            return true;
        }

        if (apis.ValueKind != JsonValueKind.Object || apis.EnumerateObject().Count() != 1)
        {
            error = "has an 'api' object that does not name exactly one API family";
            return false;
        }

        JsonProperty api = apis.EnumerateObject().Single();
        string? family = api.Name switch
        {
            "chat-completions" => ProviderApiFamilies.OpenAiChat,
            "responses" => ProviderApiFamilies.OpenAiResponses,
            "messages" => ProviderApiFamilies.AnthropicMessages,
            "generate-content" => ProviderApiFamilies.Google,
            _ => null,
        };
        if (family is null)
        {
            error = $"names unsupported API family '{api.Name}'";
            return false;
        }

        if (api.Value.ValueKind != JsonValueKind.Object)
        {
            error = $"has invalid metadata for API family '{api.Name}'";
            return false;
        }

        Uri? endpoint = null;
        if (
            api.Value.TryGetProperty("base_url", out JsonElement baseUrlValue)
            && baseUrlValue.ValueKind == JsonValueKind.String
        )
        {
            string? baseUrl = baseUrlValue.GetString();
            if (
                !Uri.TryCreate(baseUrl, UriKind.Absolute, out endpoint)
                || endpoint.Scheme is not ("https" or "http")
                || (endpoint.Scheme == "http" && !endpoint.IsLoopback)
            )
            {
                error = $"has an invalid API base_url '{baseUrl}'";
                return false;
            }

            if (
                pinToConfiguredHost
                && Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out Uri? configured)
                && !string.Equals(
                    endpoint.Host,
                    configured.Host,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                error =
                    $"has API base_url host '{endpoint.Host}' outside the custom provider's configured host";
                return false;
            }
        }

        JsonElement capabilities = Property(model, "capabilities");
        JsonElement apiCapabilities = Property(api.Value, "capabilities");
        JsonElement modalities = Property(model, "modalities");
        IReadOnlySet<string>? input = ReadModalities(modalities, "input");
        IReadOnlySet<string>? output = ReadModalities(modalities, "output");
        IReadOnlySet<string>? features = ReadFeatures(capabilities, apiCapabilities);
        JsonElement tools = Property(capabilities, "tools");
        JsonElement reasoning = Property(capabilities, "reasoning");
        JsonElement apiTools = Property(apiCapabilities, "tools");
        bool? supportsTools = ReadBoolean(tools, "supported");
        if (supportsTools is null && ReadStrings(apiTools, "choice") is { Count: > 0 })
        {
            supportsTools = true;
        }

        bool? supportsVision = input is null ? null : input.Contains("image");
        bool? supportsTemperature = ReadBoolean(capabilities, "temperature");
        int? contextWindow = ReadPositiveInt(Property(model, "limit"), "context");
        int? maxOutput = ReadPositiveInt(Property(model, "limit"), "output");
        bool? reasoningSupported = ReadBoolean(reasoning, "supported");
        string? reasoningStyle =
            reasoningSupported is not true ? null
            : ReadPositiveInt(Property(reasoning, "budget"), "max") is not null ? "budget"
            : ReadStrings(reasoning, "effort") is { Count: > 0 } ? "effort"
            : "inline";

        metadata = new DiscoveryModelMetadata(
            family,
            endpoint,
            contextWindow,
            maxOutput,
            supportsTools,
            supportsVision,
            reasoningStyle,
            supportsTemperature,
            input is null && output is null && features is null
                ? null
                : new ModelCapabilityDescriptor(
                    new HashSet<string>([family], StringComparer.Ordinal),
                    input,
                    output,
                    features
                )
        );
        return true;
    }

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
            ? value
            : default;

    private static int? ReadPositiveInt(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.Number } value
        && value.TryGetInt32(out int result)
        && result > 0
            ? result
            : null;

    private static bool? ReadBoolean(JsonElement element, string name) =>
        Property(element, name).ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    private static IReadOnlySet<string>? ReadModalities(JsonElement modalities, string name)
    {
        IReadOnlyList<string>? values = ReadStrings(modalities, name);
        return values is null
            ? null
            : new HashSet<string>(values.Select(NormalizeModality), StringComparer.Ordinal);
    }

    private static IReadOnlyList<string>? ReadStrings(JsonElement element, string name)
    {
        JsonElement values = Property(element, name);
        if (values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return
        [
            .. values
                .EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!),
        ];
    }

    private static IReadOnlySet<string>? ReadFeatures(
        JsonElement capabilities,
        JsonElement apiCapabilities
    )
    {
        if (
            capabilities.ValueKind != JsonValueKind.Object
            && apiCapabilities.ValueKind != JsonValueKind.Object
        )
        {
            return null;
        }

        var features = new HashSet<string>(StringComparer.Ordinal);
        JsonElement tools = Property(capabilities, "tools");
        JsonElement reasoning = Property(capabilities, "reasoning");
        AddIfTrue(features, "tools", ReadBoolean(tools, "supported"));
        AddIfTrue(features, "reasoning", ReadBoolean(reasoning, "supported"));
        AddIfTrue(features, "reasoning-toggle", ReadBoolean(reasoning, "toggle"));
        AddIfTrue(
            features,
            "cache-implicit",
            ReadBoolean(Property(capabilities, "cache"), "implicit")
        );
        AddIfTrue(features, "structured-output", ReadBoolean(capabilities, "structured_output"));
        AddIfTrue(features, "temperature", ReadBoolean(capabilities, "temperature"));
        AddIfTrue(features, "top-p", ReadBoolean(capabilities, "top_p"));
        AddIfTrue(features, "top-k", ReadBoolean(capabilities, "top_k"));
        AddIfTrue(features, "system-updates", ReadBoolean(capabilities, "system_updates"));
        AddIfTrue(features, "effort-updates", ReadBoolean(capabilities, "effort_updates"));

        foreach (string effort in ReadStrings(reasoning, "effort") ?? [])
        {
            features.Add($"reasoning-effort:{effort}");
        }

        JsonElement apiTools = Property(apiCapabilities, "tools");
        foreach (string choice in ReadStrings(apiTools, "choice") ?? [])
        {
            features.Add($"tool-choice:{choice}");
        }

        AddIfTrue(features, "tool-updates", ReadBoolean(apiTools, "updates"));
        return features.Count == 0 ? null : features;
    }

    private static void AddIfTrue(HashSet<string> values, string name, bool? enabled)
    {
        if (enabled is true)
        {
            values.Add(name);
        }
    }

    private static string NormalizeModality(string value) =>
        value.StartsWith("image/", StringComparison.Ordinal) ? "image"
        : value.StartsWith("video/", StringComparison.Ordinal) ? "video"
        : value.StartsWith("audio/", StringComparison.Ordinal) ? "audio"
        : value;
}
