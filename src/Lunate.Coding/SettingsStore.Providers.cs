using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The <c>providers</c> settings section: user-declared provider endpoints keyed by provider id.
/// Each entry is the provider entry v2 shape the presets use — <c>baseUrls</c> keyed by the one
/// API family the custom entry serves, a <c>credentialPlacement</c>, an optional
/// <c>modelsUrl</c> and optional <c>headers</c> — plus the model and catalog ids. The key lives
/// in the auth store under the same id. A legacy single <c>baseUrl</c> stays accepted and means
/// the <c>openai-chat</c> family; reading never rewrites a user file.
/// </summary>
public static partial class SettingsStore
{
    private static IReadOnlyList<CustomProvider> ReadProviders(
        JsonElement value,
        List<string> problems
    )
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            problems.Add("'providers' must be an object");
            return [];
        }

        List<CustomProvider> providers = [];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"'providers.{property.Name}' must be an object");
                continue;
            }

            string? baseUrl = null;
            string? model = null;
            string? catalogName = null;
            string? modelsUrl = null;
            Dictionary<ProviderApiFamily, string>? familyUrls = null;
            CredentialPlacement? placement = null;
            Dictionary<string, string>? headers = null;
            foreach (JsonProperty field in property.Value.EnumerateObject())
            {
                switch (field.Name)
                {
                    case "baseUrl":
                        baseUrl = ReadProviderField(
                            field.Value,
                            property.Name,
                            "baseUrl",
                            problems
                        );
                        break;
                    case "baseUrls":
                        familyUrls = ReadFamilyUrls(field.Value, property.Name, problems);
                        break;
                    case "model":
                        model = ReadProviderField(field.Value, property.Name, "model", problems);
                        break;
                    case "catalogName":
                        catalogName = ReadProviderField(
                            field.Value,
                            property.Name,
                            "catalogName",
                            problems
                        );
                        break;
                    case "modelsUrl":
                        modelsUrl = ReadProviderField(
                            field.Value,
                            property.Name,
                            "modelsUrl",
                            problems
                        );
                        break;
                    case "credentialPlacement":
                        if (
                            !CredentialPlacementFile.TryParseSingle(
                                field.Value,
                                out CredentialPlacement? parsed,
                                out string? placementError
                            )
                        )
                        {
                            problems.Add(
                                $"'providers.{property.Name}.credentialPlacement' {placementError}"
                            );
                        }
                        else
                        {
                            placement = parsed;
                        }

                        break;
                    case "headers":
                        headers = ReadHeaderFields(field.Value, property.Name, problems);
                        break;
                    default:
                        problems.Add($"unknown key 'providers.{property.Name}.{field.Name}'");
                        break;
                }
            }

            ProviderApiFamily family = ProviderApiFamily.OpenAiChat;
            if (familyUrls is { Count: > 0 } declared)
            {
                if (declared.Count != 1)
                {
                    problems.Add(
                        declared.Count == 0
                            ? $"'providers.{property.Name}.baseUrls' must declare one API family"
                            : $"'providers.{property.Name}' must declare one API family; found "
                                + $"{ProviderApiFamilies.NamesFor(declared.Keys)}."
                    );
                    continue;
                }

                if (
                    baseUrl is not null
                    && !string.Equals(
                        baseUrl,
                        declared.Values.First(),
                        StringComparison.Ordinal
                    )
                )
                {
                    // The shorthand and the map are two spellings of one URL; a disagreement is
                    // a contradictory file, not a precedence question.
                    problems.Add(
                        $"'providers.{property.Name}' must declare either 'baseUrl' or 'baseUrls'"
                    );
                    continue;
                }

                (family, baseUrl) = declared.First();
            }

            if (
                !CustomProvider.TryCreate(
                    property.Name,
                    baseUrl,
                    model,
                    catalogName,
                    out CustomProvider? provider,
                    out string? error,
                    ProviderApiFamilies.Name(family),
                    placement,
                    modelsUrl,
                    headers
                ) || provider is null
            )
            {
                problems.Add($"'providers.{property.Name}': {error}");
                continue;
            }

            providers.Add(provider);
        }

        return providers;
    }

    private static Dictionary<ProviderApiFamily, string>? ReadFamilyUrls(
        JsonElement value,
        string id,
        List<string> problems
    )
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"'providers.{id}.baseUrls' must be an object");
            return null;
        }

        var urls = new Dictionary<ProviderApiFamily, string>();
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!ProviderApiFamilies.TryParse(property.Name, out ProviderApiFamily family))
            {
                problems.Add(
                    $"'providers.{id}.baseUrls' has an unknown API family '{property.Name}'; "
                        + $"supported families: {ProviderApiFamilies.Supported()}."
                );
                continue;
            }

            string? url = ReadProviderField(
                property.Value,
                id,
                $"baseUrls.{property.Name}",
                problems
            );
            if (url is not null)
            {
                urls[family] = url;
            }
        }

        return urls;
    }

    private static Dictionary<string, string>? ReadHeaderFields(
        JsonElement value,
        string id,
        List<string> problems
    )
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            problems.Add($"'providers.{id}.headers' must be an object");
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty header in value.EnumerateObject())
        {
            string? headerValue = ReadProviderField(
                header.Value,
                id,
                $"headers.{header.Name}",
                problems
            );
            if (headerValue is not null)
            {
                headers[header.Name] = headerValue;
            }
        }

        return headers;
    }

    private static string? ReadProviderField(
        JsonElement value,
        string id,
        string field,
        List<string> problems
    )
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            problems.Add($"'providers.{id}.{field}' must be a string");
            return null;
        }

        return value.GetString();
    }

    /// <summary>
    /// Writes one custom provider through read-modify-write: every other settings value (including
    /// other providers) is preserved, the result is validated by the same parser, and the file is
    /// replaced atomically with <c>schemaVersion: 2</c>. The entry is the provider entry v2 shape
    /// the presets use, so <c>auth login --custom</c> and the setup wizard (#475) — the writer's
    /// only two callers — cannot drift. On any failure the old file is untouched.
    /// </summary>
    public static void WriteProvider(CustomProvider provider, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        string path = filePath ?? DefaultFilePath();
        lock (Gate)
        {
            JsonObject root = ReadRoot(path);
            root["schemaVersion"] = SupportedSchemaVersion;
            JsonObject providers = root["providers"] as JsonObject ?? new JsonObject();
            var entry = new JsonObject
            {
                ["baseUrls"] = new JsonObject
                {
                    [ProviderApiFamilies.Name(provider.ApiFamily)] = provider
                        .BaseUrl
                        .OriginalString,
                },
                ["model"] = provider.Model,
                ["catalogName"] = provider.CatalogName,
            };
            if (provider.CredentialPlacement is { } placement)
            {
                entry["credentialPlacement"] = placement.ToCompact();
            }

            if (provider.ModelsUrl is { } modelsUrl)
            {
                entry["modelsUrl"] = modelsUrl.OriginalString;
            }

            if (provider.Headers is { Count: > 0 } headers)
            {
                var declared = new JsonObject();
                foreach ((string name, string value) in headers)
                {
                    declared[name] = value;
                }

                entry["headers"] = declared;
            }

            providers[provider.Id] = entry;
            root["providers"] = providers;

            string json = root.ToJsonString(WriteOptions) + "\n";
            var problems = new List<string>();
            _ = ParseJson(json, problems);
            if (problems.Count > 0)
            {
                throw new InvalidDataException(
                    $"Invalid settings in '{path}': {string.Join("; ", problems)}. No settings were written."
                );
            }

            WriteAtomically(path, json);
        }
    }
}
