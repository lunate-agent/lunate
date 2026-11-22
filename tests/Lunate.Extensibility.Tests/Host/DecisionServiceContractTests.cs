using System.Reflection;
using Lunate.Ai.Decisions;
using Lunate.Contracts;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility.Tests;

public sealed class DecisionServiceContractTests
{
    [Fact]
    public void The_abstractions_service_references_the_contract_and_the_decision_model_only()
    {
        Assembly assembly = typeof(IDecisionService).Assembly;

        string[] lunate = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("Lunate.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        // The decision model has its own package and the edge runs down from it to the contract
        // (ADR-0045); the extension contract never reaches up into the loop or the engine.
        Assert.Equal(["Lunate.Contracts", "Lunate.Decisions.Model"], lunate);
        Assert.All(
            typeof(IDecisionService)
                .GetMethods()
                .SelectMany(method =>
                    new[] { method.ReturnType }.Concat(
                        method.GetParameters().Select(parameter => parameter.ParameterType)
                    )
                ),
            type =>
                Assert.DoesNotContain(new[] { "Provider", "Tool", "Loop" }, marker =>
                    type.FullName?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true
                )
        );
    }

    [Fact]
    public void An_extension_context_resolves_the_core_decision_service()
    {
        using var temp = new TempDirectory();
        var service = new TestDecisionService();
        var loader = new ExtensionLoader(new ExtensionHostOptions { StorePath = temp.Root });
        loader.Services.RegisterCore("core/decision", service);
        var context = new ExtensionContext(
            "test-extension",
            settings: null!,
            secrets: null!,
            log: new RecordingExtensionLog(),
            findCoreService: loader.Services.FindCore
        );

        Assert.True(context.TryGetCoreService<IDecisionService>("core/decision", out var resolved));
        Assert.Same(service, resolved);
    }

    private sealed class TestDecisionService : IDecisionService
    {
        public Task<DecisionResult> DecideAsync(
            DecisionRequest request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();
    }
}
