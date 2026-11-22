using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Extensibility.Abstractions;
using Lunate.Extensibility.Remote;
using Lunate.Extensibility.Testing;

namespace Lunate.Extensibility.Remote.Tests;

/// <summary>
/// The observable event wire (protocol version 3, ADR-0040): the inventory covers every event
/// record, each one round-trips over a real wire pair as the same <c>--json</c> line object, and the
/// contract assembly neither defines nor references a wire type.
/// </summary>
public sealed class RemoteEventConformanceTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public void The_inventory_covers_every_event_record()
    {
        Assert.Empty(RemoteEventConformance.MissingCoverage());
        string[] expected =
        [
            .. RemoteEventConformance
                .ConcreteEventTypes()
                .Select(type => type.Name)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        string[] handled =
        [
            .. RemoteEventJson
                .HandledEventTypes.Select(type => type.Name)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        Assert.Equal<IReadOnlyList<string>>(expected, handled);
    }

    [Fact]
    public void Every_sample_kind_is_in_the_published_vocabulary()
    {
        string[] sampleKinds =
        [
            .. RemoteEventConformance
                .Samples.Select(sample => RemoteEventJson.KindOf(sample.Event.Kind))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(kind => kind, StringComparer.Ordinal),
        ];

        Assert.Equal<IReadOnlyList<string>>(
            sampleKinds,
            [.. RemoteEventJson.Kinds.OrderBy(kind => kind, StringComparer.Ordinal)]
        );
    }

    [Fact]
    public async Task Every_event_record_round_trips_over_the_wire()
    {
        foreach (RemoteEventSample sample in RemoteEventConformance.Samples)
        {
            (InMemoryLineChannel a, InMemoryLineChannel b) = InMemoryLineChannel.Pair();
            await using var host = new RemoteWire(a);
            await using var child = new RemoteWire(b);
            var seen = new List<(string Method, JsonElement? Parameters)>();
            child.NotificationReceived += (method, parameters, _) =>
            {
                lock (seen)
                {
                    seen.Add((method, parameters));
                }

                return ValueTask.CompletedTask;
            };
            host.Start();
            child.Start();

            string kind = RemoteEventJson.KindOf(sample.Event.Kind);
            string line = RemoteEventJson.Serialize(sample.Event);
            using (JsonDocument document = JsonDocument.Parse(line))
            {
                await host.NotifyAsync(
                    RemoteProtocol.EventMethod(kind),
                    document.RootElement.Clone(),
                    cancellationToken: Ct
                );
            }

            await WaitAsync(() => seen.Count > 0, $"{sample.Name} to arrive");
            (string method, JsonElement? parameters) = Assert.Single(seen);
            Assert.Equal("event/" + kind, method);
            Assert.True(
                JsonNode.DeepEquals(
                    JsonNode.Parse(line),
                    JsonNode.Parse(parameters!.Value.GetRawText())
                ),
                $"{sample.Name} did not round-trip: {parameters.Value.GetRawText()}"
            );
        }
    }

    [Fact]
    public void No_wire_type_is_defined_in_or_referenced_by_the_contract_assembly()
    {
        Assembly contract = typeof(IExtensionContext).Assembly;
        string contractName = contract.GetName().Name!;

        Assert.DoesNotContain(
            contract.GetReferencedAssemblies(),
            reference => reference.Name == "Lunate.Extensibility.Remote"
        );
        Assert.DoesNotContain(
            contract.GetTypes(),
            type => type.Namespace?.StartsWith("Lunate.Extensibility.Remote", StringComparison.Ordinal) == true
        );

        Type[] wireTypes =
        [
            typeof(RemoteProtocol),
            typeof(InitializeRequest),
            typeof(InitializeResult),
            typeof(HookCallEnvelope),
            typeof(ServiceCallEnvelope),
            typeof(CommandInvokeRequest),
            typeof(CommandInvokeResult),
            typeof(RemoteEventJson),
        ];
        foreach (Type wireType in wireTypes)
        {
            Assert.NotEqual(contractName, wireType.Assembly.GetName().Name);
            Assert.Equal("Lunate.Extensibility.Remote", wireType.Assembly.GetName().Name);
        }
    }

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 250; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        Assert.Fail($"timed out waiting for {what}");
    }
}
