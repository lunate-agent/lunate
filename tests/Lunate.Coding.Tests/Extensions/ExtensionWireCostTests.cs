using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Lunate.Extensibility.Remote;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The wire's <c>costUsd</c> seam (ADR-0040): a remote observer's <c>event/usage_updated</c> params
/// are the <c>--json</c> line, so the extension host prices the record from the session catalog the
/// same way the print stream does. Without a catalog the field stays absent, never zero.
/// </summary>
public sealed class ExtensionWireCostTests
{
    [Fact]
    public void The_wire_line_is_the_json_line_with_the_priced_cost()
    {
        using var temp = new TempDirectory();
        // Two presets echo the same wire id at different prices: the record's own provider is what
        // says which entry the price belongs to, exactly as the run's selected provider does for
        // the --json line.
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "alpha/shared", "provider": "alpha", "wireId": "shared-wire", "contextWindow": 1000, "supportsTools": true, "pricing": { "input": 1000, "output": 0 } },
            { "id": "beta/shared", "provider": "beta", "wireId": "shared-wire", "contextWindow": 1000, "supportsTools": true, "pricing": { "input": 500, "output": 0 } }
            """
        );
        var usage = new UsageUpdated("run_1", new UsageDetails { InputTokenCount = 1000 })
        {
            ModelId = "shared-wire",
            Attribution = new ModelUsageAttribution("run", "profile", "beta preset", "beta"),
        };

        string printed = PrintEventJson.Serialize(usage, catalog, "beta/shared");
        string wire = RemoteEventJson.Serialize(usage, ExtensionWireCost.For(catalog));

        Assert.Equal(printed, wire);
        using JsonDocument line = JsonDocument.Parse(wire);
        Assert.Equal(0.5m, line.RootElement.GetProperty("costUsd").GetDecimal());
    }

    [Fact]
    public void The_wire_line_omits_the_cost_without_a_catalog()
    {
        var usage = new UsageUpdated(
            "run_1",
            new UsageDetails { InputTokenCount = 1000, OutputTokenCount = 500 }
        );

        string wire = RemoteEventJson.Serialize(usage, ExtensionWireCost.For(catalog: null));

        using JsonDocument line = JsonDocument.Parse(wire);
        Assert.False(line.RootElement.TryGetProperty("costUsd", out _));
    }

    [Fact]
    public void The_wire_line_omits_the_cost_for_an_unpriced_record()
    {
        using var temp = new TempDirectory();
        ModelCatalog catalog = Catalog(
            temp,
            """
            { "id": "fixture-unpriced", "provider": "fixture", "contextWindow": 1000, "supportsTools": true }
            """
        );
        var usage = new UsageUpdated("run_1", new UsageDetails { InputTokenCount = 1000 })
        {
            ModelId = "fixture-unpriced",
        };

        string wire = RemoteEventJson.Serialize(usage, ExtensionWireCost.For(catalog));

        using JsonDocument line = JsonDocument.Parse(wire);
        Assert.False(line.RootElement.TryGetProperty("costUsd", out _));
    }

    private static ModelCatalog Catalog(TempDirectory temp, string models)
    {
        string path = temp.File("models.json");
        File.WriteAllText(path, $$"""{ "schemaVersion": 2, "models": [ {{models}} ] }""");
        return ModelCatalog.Load(path);
    }
}
