using System.Security.Cryptography;
using System.Text.Json;

namespace Lunate.Agent.Tests;

public sealed class SystemOneOpenApiContractTests
{
    private const string OpenApiFile = "typesafe-openapi.json";
    private const string OpenApiHash =
        "3ca93b6e6b9aa8d7820531b78c66ed7a9798b11e6e163ad96b140fb3d75fbcf4";
    private static readonly string FixtureDirectory = Path.Combine(
        TestPaths.RepositoryRoot,
        "tests",
        "fixtures",
        "systemone"
    );

    public static TheoryData<string, string> ResponseFixtures =>
        new()
        {
            { "openrouter-noul-choice-score.json", "SystemOneResponse" },
            { "openrouter-box.json", "SystemOneResponse" },
            { "opencode-jev113-noul.json", "SystemOneResponse" },
            { "openrouter-unknown-model.json", "HTTPBadRequestError" },
            { "openrouter-unknown-question-type.json", "HTTPBadRequestError" },
            { "openrouter-score-criteria-array-required.json", "HTTPBadRequestError" },
            { "openrouter-choice-criteria-object-required.json", "HTTPBadRequestError" },
            { "opencode-images-need-header.json", "HTTPBadRequestError" },
            { "opencode-jev114-unavailable.json", "HTTPBadRequestError" },
        };

    [Fact]
    public void Published_openapi_snapshot_matches_its_pinned_hash()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(FixtureDirectory, OpenApiFile));

        Assert.Equal(OpenApiHash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Theory]
    [MemberData(nameof(ResponseFixtures))]
    public void Recorded_response_fixture_matches_the_published_openapi(
        string fileName,
        string responseSchema
    )
    {
        using JsonDocument openApi = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDirectory, OpenApiFile))
        );
        using JsonDocument fixture = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDirectory, fileName))
        );
        JsonElement instance = fixture.RootElement;
        if (responseSchema == "HTTPBadRequestError" && !instance.TryGetProperty("detail", out _))
        {
            JsonElement providerError = instance.GetProperty("error");
            using JsonDocument normalized = JsonDocument.Parse(
                JsonSerializer.Serialize(new { detail = providerError })
            );
            AssertSchema(normalized.RootElement, responseSchema, openApi.RootElement, fileName);
            return;
        }

        AssertSchema(instance, responseSchema, openApi.RootElement, fileName);
    }

    private static void AssertSchema(
        JsonElement instance,
        string schemaName,
        JsonElement openApi,
        string fileName
    )
    {
        JsonElement schema = openApi
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty(schemaName);
        Assert.True(
            Matches(instance, schema, openApi, "$", out string reason),
            $"Fixture '{fileName}' does not match '#/components/schemas/{schemaName}': {reason}"
        );
    }

    private static bool Matches(
        JsonElement instance,
        JsonElement schema,
        JsonElement openApi,
        string path,
        out string reason
    )
    {
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            schema = ResolveReference(openApi, reference.GetString()!);
        }

        if (schema.TryGetProperty("oneOf", out JsonElement oneOf))
        {
            int matches = oneOf
                .EnumerateArray()
                .Count(item => Matches(instance, item, openApi, path, out _));
            reason = $"oneOf matched {matches} alternatives";
            return matches == 1;
        }

        if (schema.TryGetProperty("anyOf", out JsonElement anyOf))
        {
            bool matches = anyOf
                .EnumerateArray()
                .Any(item => Matches(instance, item, openApi, path, out _));
            reason = matches ? string.Empty : $"{path} matches no anyOf alternative";
            return matches;
        }

        if (schema.TryGetProperty("type", out JsonElement type) && !HasType(instance, type))
        {
            reason = $"{path} has JSON type {instance.ValueKind}, not {type.GetRawText()}";
            return false;
        }

        if (
            schema.TryGetProperty("const", out JsonElement constant)
            && !JsonElement.DeepEquals(instance, constant)
        )
        {
            reason = $"{path} does not equal its schema const";
            return false;
        }

        if (
            schema.TryGetProperty("enum", out JsonElement values)
            && !values.EnumerateArray().Any(value => JsonElement.DeepEquals(instance, value))
        )
        {
            reason = $"{path} is not in its schema enum";
            return false;
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out JsonElement required))
            {
                foreach (JsonElement field in required.EnumerateArray())
                {
                    if (!instance.TryGetProperty(field.GetString()!, out _))
                    {
                        reason = $"{path} is missing required property '{field.GetString()}'";
                        return false;
                    }
                }
            }

            JsonElement properties = default;
            _ = schema.TryGetProperty("properties", out properties);
            JsonElement additional = default;
            bool hasAdditional = schema.TryGetProperty("additionalProperties", out additional);
            foreach (JsonProperty property in instance.EnumerateObject())
            {
                string propertyPath = $"{path}.{property.Name}";
                if (
                    properties.ValueKind == JsonValueKind.Object
                    && properties.TryGetProperty(property.Name, out JsonElement propertySchema)
                )
                {
                    if (!Matches(property.Value, propertySchema, openApi, propertyPath, out reason))
                    {
                        return false;
                    }
                }
                else if (hasAdditional && additional.ValueKind == JsonValueKind.False)
                {
                    reason = $"{path} has unexpected property '{property.Name}'";
                    return false;
                }
                else if (hasAdditional && additional.ValueKind == JsonValueKind.Object)
                {
                    if (!Matches(property.Value, additional, openApi, propertyPath, out reason))
                    {
                        return false;
                    }
                }
            }
        }

        if (
            instance.ValueKind == JsonValueKind.Array
            && schema.TryGetProperty("items", out JsonElement itemSchema)
        )
        {
            int index = 0;
            foreach (JsonElement item in instance.EnumerateArray())
            {
                if (!Matches(item, itemSchema, openApi, $"{path}[{index}]", out reason))
                {
                    return false;
                }

                index++;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static JsonElement ResolveReference(JsonElement openApi, string reference)
    {
        JsonElement current = openApi;
        foreach (string part in reference[2..].Split('/'))
        {
            string name = part.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            current = current.GetProperty(name);
        }

        return current;
    }

    private static bool HasType(JsonElement instance, JsonElement type) =>
        type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Any(value => HasType(instance, value))
            : type.GetString() switch
            {
                "object" => instance.ValueKind == JsonValueKind.Object,
                "array" => instance.ValueKind == JsonValueKind.Array,
                "string" => instance.ValueKind == JsonValueKind.String,
                "number" => instance.ValueKind == JsonValueKind.Number,
                "integer" => instance.ValueKind == JsonValueKind.Number
                    && instance.TryGetInt64(out _),
                "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => instance.ValueKind == JsonValueKind.Null,
                _ => true,
            };
}
