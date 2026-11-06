using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

public sealed class ResumedDecisionCostTests
{
    [Fact]
    public async Task Cost_after_resume_keeps_the_provider_reported_decision_amount()
    {
        using var temp = new TempDirectory();
        // The catalog prices the decision model differently from the provider's report, so the
        // resumed report proves which source the record kept.
        File.WriteAllText(
            temp.File("models.json"),
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "decision-model", "provider": "openai", "contextWindow": 64000, "supportsTools": false, "pricing": { "input": 1, "output": 2 } }
              ]
            }
            """
        );
        var attribution = new ModelUsageAttribution(
            "decision",
            "decision",
            "router",
            "openai",
            "native",
            null
        );
        string path = temp.File("prewritten.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "hi"));
        session.AppendModelUsage(
            "decision-model",
            new SessionUsage(1000, 100) { Attribution = attribution, Cost = 0.0123m },
            attribution
        );
        using var host = new InteractiveSessionHost(
            launch: new LaunchOptions { Model = "decision-model" },
            startSession: path,
            modelsPath: temp.File("models.json")
        );
        host.Scrollback.Profile.Width = 400;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/cost");
        host.Console.SendEnter();
        string report = await InteractiveSessionCommandTests.WaitForScrollbackAsync(
            host,
            "cost by usage:"
        );

        Assert.Contains("decision (native)", report, StringComparison.Ordinal);
        Assert.Contains("$0.0123 (provider-reported)", report, StringComparison.Ordinal);
        Assert.DoesNotContain("$0.0012", report, StringComparison.Ordinal);

        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task Cost_after_resume_prices_a_record_without_a_provider_amount()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("models.json"),
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "decision-model", "provider": "openai", "contextWindow": 64000, "supportsTools": false, "pricing": { "input": 1, "output": 2 } }
              ]
            }
            """
        );
        var attribution = new ModelUsageAttribution(
            "decision",
            "decision",
            "router",
            "openai",
            "adapter",
            null
        );
        string path = temp.File("prewritten.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "hi"));
        session.AppendModelUsage(
            "decision-model",
            new SessionUsage(1000, 100) { Attribution = attribution },
            attribution
        );
        using var host = new InteractiveSessionHost(
            launch: new LaunchOptions { Model = "decision-model" },
            startSession: path,
            modelsPath: temp.File("models.json")
        );
        host.Scrollback.Profile.Width = 400;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/cost");
        host.Console.SendEnter();
        string report = await InteractiveSessionCommandTests.WaitForScrollbackAsync(
            host,
            "cost by usage:"
        );

        Assert.Contains("decision (adapter)", report, StringComparison.Ordinal);
        Assert.Contains("$0.0012 (estimate)", report, StringComparison.Ordinal);

        host.Console.Complete();
        await run;
    }
}
