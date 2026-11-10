using System.Reflection;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The inventory behind finding N-1: the duplicate Ctrl+C quit window and the unused decision-client
/// service extension must stay gone. Both checks read the built assemblies, so a reintroduced type
/// fails here even when nothing references it.
/// </summary>
public sealed class DeadCodeInventoryTests
{
    [Fact]
    public void The_tui_assembly_declares_exactly_one_quit_window_type()
    {
        Type quitWindow = Assert.Single(
            typeof(QuitWindow).Assembly.GetTypes(),
            type => type.Name.EndsWith("QuitWindow", StringComparison.Ordinal)
        );

        Assert.Equal("Lunate.Tui.QuitWindow", quitWindow.FullName);
    }

    [Fact]
    public void The_decisions_assembly_declares_no_decision_client_service_extensions()
    {
        Assembly decisions = Assembly.Load("Lunate.Ai.Decisions");

        Assert.Null(
            decisions.GetType(
                "Lunate.Ai.Decisions.DecisionClientServiceExtensions",
                throwOnError: false
            )
        );
    }
}
