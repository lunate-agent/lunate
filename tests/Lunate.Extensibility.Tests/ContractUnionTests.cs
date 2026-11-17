using System.Reflection;
using System.Runtime.CompilerServices;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility.Tests;

/// <summary>
/// The contract's closed sets are C# 15 unions (ADR-0043): the kind or case set is fixed at compile
/// time, so no assembly outside the contract can add one, a consumer's <c>switch</c> is checked for
/// exhaustiveness, and the cases stay the record types an emitter constructs.
/// </summary>
public sealed class ContractUnionTests
{
    public static TheoryData<Type> Unions =>
        new()
        {
            typeof(AgentEvent),
            typeof(ExtensionEvent),
            typeof(ProjectTrustResult),
            typeof(InputReceivedResult),
            typeof(RunStartingResult),
            typeof(MessageCompletedResult),
            typeof(ToolCallingResult),
            typeof(TurnEndedResult),
            typeof(CompactingResult),
        };

    [Theory]
    [MemberData(nameof(Unions))]
    public void Every_contract_set_is_a_union(Type union)
    {
        Assert.True(typeof(IUnion).IsAssignableFrom(union), $"{union.Name} is not a union.");
        Assert.True(
            union.IsValueType,
            $"{union.Name} is not the union wrapper the contract declares."
        );
        Assert.NotEmpty(Cases(union));
        Assert.All(Cases(union), @case => Assert.Same(union.Assembly, @case.Assembly));
    }

    [Fact]
    public void The_event_union_covers_every_declared_event_record()
    {
        Type[] records =
        [
            .. typeof(AgentEvent)
                .Assembly.GetExportedTypes()
                .Where(type => typeof(AgentEventBase).IsAssignableFrom(type) && type.IsSealed),
        ];
        Type[] cases = [.. Cases(typeof(AgentEvent))];

        Assert.Equal(
            records.OrderBy(type => type.Name, StringComparer.Ordinal),
            cases.OrderBy(type => type.Name, StringComparer.Ordinal)
        );
    }

    [Fact]
    public void The_extension_event_union_is_the_extension_half_of_the_event_union()
    {
        Type[] extensionCases = [.. Cases(typeof(ExtensionEvent))];
        Type[] eventCases = [.. Cases(typeof(AgentEvent))];

        Assert.NotEmpty(extensionCases);
        Assert.All(extensionCases, @case => Assert.Contains(@case, eventCases));
        Assert.All(extensionCases, @case =>
            Assert.True(typeof(ExtensionEventBase).IsAssignableFrom(@case))
        );
    }

    [Fact]
    public void A_union_event_reports_its_concrete_record_and_carries_the_base_state()
    {
        AgentEvent started = new RunStarted("run_1") { SessionId = "session_1" };

        Assert.Same(typeof(RunStarted), started.Kind);
        Assert.Equal("run_1", started.RunId);
        Assert.Equal("session_1", started.SessionId);
        Assert.Equal("core", started.Source);
        Assert.Equal((AgentEvent)new RunStarted("run_1") { SessionId = "session_1" }, started);
        Assert.Single(new[] { started }.OfType<RunStarted>());
    }

    [Fact]
    public void The_extension_event_union_folds_into_the_event_union()
    {
        ExtensionEvent progress = new ToolProgressUpdate("run_1", "call_1", "half way");
        AgentEvent folded = progress;

        Assert.Same(typeof(ToolProgressUpdate), folded.Kind);
        Assert.True(folded.Value is ExtensionEventBase);
        Assert.False(((AgentEvent)new RunStarted("run_1")).Value is ExtensionEventBase);
    }

    [Fact]
    public void Every_hook_result_family_is_a_union_of_marker_cases()
    {
        Type[] families =
        [
            typeof(ProjectTrustResult),
            typeof(InputReceivedResult),
            typeof(RunStartingResult),
            typeof(MessageCompletedResult),
            typeof(ToolCallingResult),
            typeof(TurnEndedResult),
            typeof(CompactingResult),
        ];

        foreach (Type family in families)
        {
            Assert.True(
                typeof(IHookResult).IsAssignableFrom(family),
                $"{family.Name} no longer implements IHookResult."
            );

            Type[] cases = [.. Cases(family)];
            Assert.NotEmpty(cases);
            Assert.All(cases, @case => Assert.Contains(typeof(IHookResult), @case.GetInterfaces()));
        }
    }

    [Fact]
    public void A_union_result_matches_its_case()
    {
        ToolCallingResult block = new ToolCallingResult.Block("held");

        Assert.True(block.Value is ToolCallingResult.Block);
        Assert.Equal(
            "held",
            block switch
            {
                ToolCallingResult.Block value => value.Reason,
                ToolCallingResult.Proceed => "proceeded",
                _ => "no case",
            }
        );
    }

    /// <summary>One constructor per case type is how a union declares its case set.</summary>
    private static IEnumerable<Type> Cases(Type union) =>
        union
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor =>
                constructor.GetParameters() is [var parameter]
                && parameter.ParameterType != typeof(AgentEvent)
            )
            .Select(constructor => constructor.GetParameters()[0].ParameterType);
}
