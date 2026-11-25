using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// A configured backend's credential is a reference resolved at send time, the way a provider
/// credential is: <c>auth:</c> from the auth store, <c>env:</c> from the environment, and
/// <c>helper:</c> refused because this host cannot resolve one. A reference that does not resolve is
/// an error the model can act on, and it sends no request.
/// </summary>
public sealed class WebSearchCredentialTests
{
    [Fact]
    public void An_auth_reference_resolves_from_the_named_key_source()
    {
        bool resolved = WebSearchCredential.TryResolve(
            "auth:brave",
            name => name == "brave" ? "stored-key" : null,
            _ => null,
            out string? value,
            out string? error
        );

        Assert.True(resolved, error);
        Assert.Equal("stored-key", value);
    }

    [Fact]
    public void An_env_reference_resolves_from_the_environment()
    {
        bool resolved = WebSearchCredential.TryResolve(
            "env:BRAVE_SEARCH_KEY",
            namedKeySource: null,
            name => name == "BRAVE_SEARCH_KEY" ? "env-key" : null,
            out string? value,
            out string? error
        );

        Assert.True(resolved, error);
        Assert.Equal("env-key", value);
    }

    [Fact]
    public void A_helper_reference_is_refused_with_the_alternatives()
    {
        bool resolved = WebSearchCredential.TryResolve(
            "helper:keychain",
            namedKeySource: null,
            _ => null,
            out string? value,
            out string? error
        );

        Assert.False(resolved);
        Assert.Null(value);
        Assert.Contains("helper:keychain", error!, StringComparison.Ordinal);
        Assert.Contains(WebSearchCredential.SettingKey, error, StringComparison.Ordinal);
        Assert.Contains("auth:<name>", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("env:MISSING_SEARCH_KEY", "MISSING_SEARCH_KEY", "try again")]
    [InlineData("auth:missing", "missing", "auth.json")]
    public async Task An_unresolved_reference_names_the_setting_and_sends_no_request(
        string reference,
        string named,
        string next
    )
    {
        using var handler = StubHttpHandler.Json("{}");
        var tool = new WebSearchTool(
            new WebSearchConfiguration(
                new WebSearchSettings
                {
                    Backend = WebSearchBackendKind.Brave,
                    Credential = reference,
                },
                _ => null,
                _ => null
            ),
            handler: handler,
            timeout: WaitBudgets.WebStubRequest
        );

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(WebSearchCredential.SettingKey, result.Output, StringComparison.Ordinal);
        Assert.Contains(reference, result.Output, StringComparison.Ordinal);
        Assert.Contains(named, result.Output, StringComparison.Ordinal);
        Assert.Contains(next, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_backend_that_needs_a_credential_without_one_names_the_setting()
    {
        using var handler = StubHttpHandler.Json("{}");
        var tool = new WebSearchTool(
            new WebSearchConfiguration(
                new WebSearchSettings { Backend = WebSearchBackendKind.Tavily }
            ),
            handler: handler,
            timeout: WaitBudgets.WebStubRequest
        );

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains("Tavily", result.Output, StringComparison.Ordinal);
        Assert.Contains(WebSearchCredential.SettingKey, result.Output, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }
}
