using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

public sealed class DecisionApprovalPromptTests
{
    [Fact]
    public async Task Escalated_approval_prompt_shows_decision_reason()
    {
        var decision = new ScriptedChatClient().Enqueue(
            Scripts.Text(
                """{"results":{"writes_outside_workspace":{"answer":"yes","probabilities":{"yes":0.87,"no":0.13}},"network_access":{"answer":"no","probabilities":{"yes":0.05,"no":0.95}},"irreversible_deletion":{"answer":"no","probabilities":{"yes":0.05,"no":0.95}},"credential_exposure":{"answer":"no","probabilities":{"yes":0.05,"no":0.95}}}}"""
            ),
            Scripts.Stop()
        );
        using var host = new InteractiveSessionHost(configureHarness: options =>
        {
            var usageModels = options.UsageModels!.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal
            );
            usageModels["decision"] = new UsageModelClient(
                decision,
                "decision-profile",
                "openai",
                new ModelInfo("decision-model", "openai", null, 64000, SupportsTools: false)
            );
            return options with { DecisionMode = DecisionMode.Enforce, UsageModels = usageModels };
        });
        host.Client.Enqueue(
            Scripts.Call("call-1", "write", Scripts.Args(("path", "note.txt"), ("content", "hi"))),
            Scripts.ToolCalls()
        );
        host.Client.Enqueue(Scripts.Text("done"), Scripts.Stop());
        Task run = host.RunAsync();
        await host.SendLineAsync("go");
        await host.WaitForApprovalAsync();

        const string reason = "decision model recommends deny (0.87, decision-model)";
        Assert.Contains(reason, host.Session.PendingApproval!.Detail!);
        await host.WaitForFrameAsync(reason);
        Assert.Contains(reason, host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("y");
        await host.Client.WaitForCallAsync(2);
        host.Console.Complete();
        await run;
    }
}
