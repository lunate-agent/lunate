using System.Reflection;
using System.Runtime.CompilerServices;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility.Tests;

/// <summary>
/// The contract's closed sets are closed in the compiler (ADR-0042): the set of event kinds and
/// hook-result cases is fixed at compile time, so no assembly outside the contract can add one and
/// a consumer's switch is checked for exhaustiveness (CS8509). A union wrapper is not the shape
/// these families take — its <c>Value</c> is <c>object</c>, which would erase the base that carries
/// the stamped run fields or the <see cref="IHookResult"/> marker.
/// </summary>
public sealed class ContractClosedSetTests
{
    public static TheoryData<Type> ClosedRoots =>
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
    [MemberData(nameof(ClosedRoots))]
    public void Every_closed_root_carries_the_compilers_closed_marker(Type root)
    {
        Assert.NotNull(root.GetCustomAttribute<IsClosedTypeAttribute>(inherit: false));
    }

    [Theory]
    [MemberData(nameof(ClosedRoots))]
    public void No_closed_root_is_a_union_wrapper(Type root)
    {
        Assert.False(
            typeof(IUnion).IsAssignableFrom(root),
            $"{root.Name} is a union; the contract's closed sets are closed hierarchies (ADR-0042)."
        );
    }

    [Fact]
    public void Every_hook_result_family_keeps_its_marker_and_nested_sealed_cases()
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

            Type[] cases = [.. family.GetNestedTypes(BindingFlags.Public)];
            Assert.NotEmpty(cases);
            Assert.All(cases, @case =>
            {
                Assert.True(@case.IsSealed, $"{@case.Name} is not a sealed case.");
                Assert.Equal(family, @case.BaseType);
                Assert.Contains(typeof(IHookResult), @case.GetInterfaces());
            });
        }
    }

    [Fact]
    public void Every_declared_event_kind_is_a_sealed_record_of_the_contract()
    {
        Type[] kinds =
        [
            .. typeof(AgentEvent)
                .Assembly.GetExportedTypes()
                .Where(type =>
                    typeof(AgentEvent).IsAssignableFrom(type) && type != typeof(AgentEvent)
                ),
        ];

        Assert.NotEmpty(kinds);
        Assert.All(kinds, kind =>
        {
            Assert.True(
                kind.IsSealed || kind == typeof(ExtensionEvent),
                $"{kind.Name} is neither sealed nor the closed extension base."
            );
            Assert.Same(typeof(AgentEvent).Assembly, kind.Assembly);
        });
    }
}
