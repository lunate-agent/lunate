using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The web tools sit on the <c>bash</c> approval tier: Execute risk, always declined by an
/// unattended policy, declined per call by print mode, and allowed only by the explicit yolo flag.
/// A declined call never turns into text a model can act on without being told why.
/// </summary>
public sealed class WebToolApprovalTests
{
    [Fact]
    public void Both_web_tools_declare_execute_risk()
    {
        Assert.Equal(ToolRisk.Execute, new WebSearchTool().Risk);
        Assert.Equal(ToolRisk.Execute, new WebFetchTool().Risk);
        Assert.Equal("web_search", new WebSearchTool().Name);
        Assert.Equal("web_fetch", new WebFetchTool().Name);
    }

    [Theory]
    [InlineData(ApprovalPolicy.Ask)]
    [InlineData(ApprovalPolicy.AutoEdit)]
    public async Task No_unattended_policy_allows_a_web_call(ApprovalPolicy policy)
    {
        var approver = new NonInteractiveApprover(policy, yolo: false);

        Assert.False(
            await approver.ApproveAsync(
                Request(new WebSearchTool()),
                TestContext.Current.CancellationToken
            )
        );
        Assert.False(
            await approver.ApproveAsync(
                Request(new WebFetchTool()),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Yolo_allows_a_web_call()
    {
        var approver = new NonInteractiveApprover(ApprovalPolicy.Ask, yolo: true);

        Assert.True(
            await approver.ApproveAsync(
                Request(new WebFetchTool()),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Theory]
    [InlineData(null, "ask")]
    [InlineData("auto-edit", "auto-edit")]
    public async Task Print_mode_declines_a_web_call_before_it_runs(string? approval, string policy)
    {
        using var temp = new TempDirectory();
        var client = new ScriptedChatClient()
            .Enqueue(
                Scripts.Call("call-1", "web_fetch", Scripts.Args(("url", "https://example.test/"))),
                Scripts.ToolCalls()
            )
            .Enqueue(Scripts.Text("Cannot."), Scripts.Stop());
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Fetch that page.",
            Environment = PrintModeTestSupport.Environment(approval: approval),
        };

        (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(0, exitCode);
        Assert.Equal("Cannot.", output);
        Assert.Equal(
            $"tool 'web_fetch' denied: approval policy '{policy}' (pass --yolo to run unattended)\n",
            errors
        );
        FunctionResultContent result = Assert.Single(
            client.Requests[1][^1].Contents.OfType<FunctionResultContent>()
        );
        Assert.StartsWith(
            "Denied: 'web_fetch' was not run.",
            result.Result!.ToString()!,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task A_search_call_is_declined_the_same_way()
    {
        using var temp = new TempDirectory();
        var client = new ScriptedChatClient()
            .Enqueue(
                Scripts.Call("call-1", "web_search", Scripts.Args(("query", "dotnet"))),
                Scripts.ToolCalls()
            )
            .Enqueue(Scripts.Text("Cannot."), Scripts.Stop());
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(
            temp,
            new FakeChatClientFactory(client)
        ) with
        {
            Prompt = "Search.",
        };

        (int exitCode, _, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(0, exitCode);
        Assert.Contains("tool 'web_search' denied", errors, StringComparison.Ordinal);
        FunctionResultContent result = Assert.Single(
            client.Requests[1][^1].Contents.OfType<FunctionResultContent>()
        );
        Assert.StartsWith(
            "Denied: 'web_search' was not run.",
            result.Result!.ToString()!,
            StringComparison.Ordinal
        );
    }

    private static ApprovalRequest Request(ITool tool) =>
        new(
            tool,
            WebTestSupport.Args("""{"url":"https://example.test/","query":"x"}"""),
            "run_1",
            null,
            0
        );
}
