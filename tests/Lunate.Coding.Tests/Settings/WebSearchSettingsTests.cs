namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>tools.webSearch</c> settings section: DuckDuckGo is the default, SearXNG needs a URL,
/// Brave and Tavily need a credential reference, and every contradiction is refused by key at load.
/// A project settings file cannot select a backend, so a repository cannot redirect a search or
/// spend the user's credential.
/// </summary>
public sealed class WebSearchSettingsTests
{
    [Fact]
    public void Missing_settings_default_to_DuckDuckGo()
    {
        using var temp = new TempDirectory();

        AgentSettings settings = SettingsStore.Resolve(temp.File("missing.json"), _ => null);

        Assert.Equal(WebSearchBackendKind.DuckDuckGo, settings.Tools.WebSearch.Backend);
        Assert.Null(settings.Tools.WebSearch.Url);
        Assert.Null(settings.Tools.WebSearch.Credential);
    }

    [Fact]
    public void A_SearXNG_section_resolves_its_loopback_url_without_a_credential()
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            """
            {
              "schemaVersion": 2,
              "tools": {
                "webSearch": { "backend": "searxng", "url": "http://127.0.0.1:8888/" }
              }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

        Assert.Equal(WebSearchBackendKind.SearXng, settings.Tools.WebSearch.Backend);
        Assert.Equal("http://127.0.0.1:8888/", settings.Tools.WebSearch.Url?.AbsoluteUri);
        Assert.Null(settings.Tools.WebSearch.Credential);
    }

    [Theory]
    [InlineData("brave", "auth:brave")]
    [InlineData("tavily", "env:TAVILY_API_KEY")]
    public void A_Brave_or_Tavily_section_keeps_its_reference_unresolved(
        string backend,
        string reference
    )
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            $$"""
            {
              "schemaVersion": 2,
              "tools": {
                "webSearch": { "backend": "{{backend}}", "credential": "{{reference}}" }
              }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

        Assert.Equal(
            backend == "brave" ? WebSearchBackendKind.Brave : WebSearchBackendKind.Tavily,
            settings.Tools.WebSearch.Backend
        );
        Assert.Equal(reference, settings.Tools.WebSearch.Credential);
    }

    [Theory]
    [InlineData("bing", "'duckduckgo', 'searxng', 'brave' or 'tavily'")]
    [InlineData("DuckDuckGo", "'duckduckgo', 'searxng', 'brave' or 'tavily'")]
    public void An_unknown_backend_names_the_accepted_values(string backend, string accepted)
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            $$"""
            {
              "schemaVersion": 2,
              "tools": { "webSearch": { "backend": "{{backend}}" } }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains("'tools.webSearch.backend'", exception.Message, StringComparison.Ordinal);
        Assert.Contains(accepted, exception.Message, StringComparison.Ordinal);
        Assert.Contains("No settings were applied", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "backend": "searxng" }""", "tools.webSearch.url' is required")]
    [InlineData("""{ "backend": "brave" }""", "tools.webSearch.credential' is required")]
    [InlineData(
        """{ "backend": "duckduckgo", "url": "https://search.example.test/" }""",
        "belongs to the 'searxng' backend"
    )]
    [InlineData(
        """{ "backend": "duckduckgo", "credential": "env:KEY" }""",
        "belongs to the 'searxng', 'brave' and 'tavily'"
    )]
    [InlineData(
        """{ "backend": "brave", "credential": "env:KEY", "url": "https://search.example.test/" }""",
        "belongs to the 'searxng' backend"
    )]
    public void Missing_and_irrelevant_keys_are_refused_by_key(string section, string expected)
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            $$"""
            {
              "schemaVersion": 2,
              "tools": { "webSearch": {{section}} }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://search.example.test/")]
    [InlineData("http://search.example.test/")]
    [InlineData("relative/path")]
    public void A_SearXNG_url_must_be_https_or_loopback_http(string url)
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            $$"""
            {
              "schemaVersion": 2,
              "tools": {
                "webSearch": { "backend": "searxng", "url": "{{url}}" }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            "'tools.webSearch.url' must be an absolute https URL",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("sk-live-1234")]
    [InlineData("Bearer sk-live-1234")]
    [InlineData("env:")]
    public void A_literal_secret_is_not_a_credential_reference(string credential)
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            $$"""
            {
              "schemaVersion": 2,
              "tools": {
                "webSearch": { "backend": "brave", "credential": "{{credential}}" }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            "'tools.webSearch.credential'",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("auth:<name>", exception.Message, StringComparison.Ordinal);
        Assert.Contains("env:<VARIABLE>", exception.Message, StringComparison.Ordinal);
        Assert.Contains("helper:<name>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_key_under_webSearch_is_refused()
    {
        using var temp = new TempDirectory();
        Write(
            temp,
            """
            {
              "schemaVersion": 2,
              "tools": { "webSearch": { "backend": "duckduckgo", "apiKey": "x" } }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(temp.File("settings.json"), _ => null)
        );

        Assert.Contains(
            "unknown key 'tools.webSearch.apiKey'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void A_project_settings_file_cannot_select_a_backend()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("user-settings.json"), """{ "schemaVersion": 2 }""");
        Write(
            temp,
            """
            {
              "schemaVersion": 2,
              "tools": { "webSearch": { "backend": "brave", "credential": "env:KEY" } }
            }
            """,
            "project-settings.json"
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.ResolveWithProject(
                temp.File("user-settings.json"),
                temp.File("project-settings.json"),
                [],
                _ => null
            )
        );

        Assert.Contains("'tools' is not allowed", exception.Message, StringComparison.Ordinal);
    }

    private static void Write(TempDirectory temp, string json, string name = "settings.json") =>
        File.WriteAllText(temp.File(name), json);
}
