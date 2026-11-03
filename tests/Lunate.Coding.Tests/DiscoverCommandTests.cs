using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

public sealed class DiscoverCommandTests
{
    private const string UrlTarget = "http://localhost:9999/v1";

    [Fact]
    public async Task Url_target_requests_models_and_returns_a_draft_without_writing_anything()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(
                HttpStatusCode.OK,
                """{ "data": [ { "id": "alpha" }, { "id": "beta" } ], "extra": 1 }"""
            )
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            UrlTarget,
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal($"{UrlTarget}/models", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.False(handler.Requests[0].Headers.Contains("Authorization"));
        Assert.Empty(Directory.GetFileSystemEntries(temp.Root));

        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        Assert.Equal(2, draft.RootElement.GetProperty("schemaVersion").GetInt32());
        JsonElement models = draft.RootElement.GetProperty("models");
        Assert.Equal(["alpha", "beta"], Ids(models));
        Assert.Equal(UrlTarget, models[0].GetProperty("endpoint").GetString());
        Assert.False(models[0].TryGetProperty("provider", out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Url_target_sends_no_credential_even_when_a_key_is_configured(
        bool fromEnvironment
    )
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "alpha" } ] }""")
        );
        bool storeRead = false;

        DiscoverResult result = await DiscoverCommand.RunAsync(
            UrlTarget,
            Catalog(temp),
            handler,
            namedKeySource: _ =>
            {
                storeRead = true;
                return "stored-key";
            },
            environment: name => fromEnvironment && name == "OPENAI_API_KEY" ? "env-key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        AssertNoCredential(handler.Requests[0]);
        Assert.False(storeRead);
    }

    [Theory]
    [InlineData("https://api.openai.com/v1", "OPENAI_API_KEY", "openai-key", "Authorization")]
    [InlineData("https://api.anthropic.com/v1", "ANTHROPIC_API_KEY", "anthropic-key", "x-api-key")]
    public async Task A_direct_preset_url_uses_that_presets_key_and_header(
        string url,
        string variable,
        string key,
        string header
    )
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "alpha" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            url,
            Catalog(temp),
            handler,
            environment: name => name == variable ? key : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal($"{url}/models", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal(
            header == "Authorization" ? $"Bearer {key}" : key,
            handler.Requests[0].Headers.GetValues(header).Single()
        );
        Assert.False(
            handler
                .Requests[0]
                .Headers.Contains(header == "Authorization" ? "x-api-key" : "Authorization")
        );
    }

    [Fact]
    public async Task A_declared_preset_headers_and_the_lunate_user_agent_go_out_on_discovery()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "alpha" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "openrouter",
            Catalog(temp),
            handler,
            environment: name => name == "OPENROUTER_API_KEY" ? "key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        HttpRequestMessage request = handler.Requests[0];
        // The provider's documented attribution headers reach discovery the same way they reach
        // chat, and the request names the client that made it.
        Assert.StartsWith("lunate/", request.Headers.UserAgent.ToString());
        Assert.Equal(
            "https://github.com/wdaniel1993/lunate",
            request.Headers.GetValues("HTTP-Referer").Single()
        );
        Assert.Equal("Lunate", request.Headers.GetValues("X-Title").Single());
    }

    [Fact]
    public async Task Name_target_uses_the_catalog_endpoint_and_provider()
    {
        using var temp = new TempDirectory();
        string userFile = WriteCatalog(
            temp,
            """
            { "id": "local", "provider": "openai", "endpoint": "http://localhost:4321/v1", "contextWindow": 8192, "supportsTools": true }
            """
        );
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "tiny" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "local",
            ModelCatalog.Load(userFile),
            handler,
            environment: name => name == "OPENAI_API_KEY" ? "env-key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "http://localhost:4321/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        AssertNoCredential(handler.Requests[0]);
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement model = draft.RootElement.GetProperty("models")[0];
        Assert.Equal("openai", model.GetProperty("provider").GetString());
        Assert.Equal("http://localhost:4321/v1", model.GetProperty("endpoint").GetString());
    }

    [Fact]
    public async Task A_declared_gateway_endpoint_gets_no_credential()
    {
        using var temp = new TempDirectory();
        string userFile = WriteCatalog(
            temp,
            """
            { "id": "gateway", "provider": "openai", "endpoint": "http://gateway.test/v1", "contextWindow": 8192, "supportsTools": true }
            """
        );
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "small" } ] }""")
        );
        bool storeRead = false;

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "gateway",
            ModelCatalog.Load(userFile),
            handler,
            namedKeySource: _ =>
            {
                storeRead = true;
                return "stored-openai-key";
            },
            environment: name => name == "OPENAI_API_KEY" ? "env-key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("http://gateway.test/v1/models", handler.Requests[0].RequestUri!.AbsoluteUri);
        AssertNoCredential(handler.Requests[0]);
        Assert.False(storeRead);
    }

    [Fact]
    public async Task An_unknown_provider_endpoint_gets_no_credential()
    {
        using var temp = new TempDirectory();
        string userFile = WriteCatalog(
            temp,
            """
            { "id": "custom", "provider": "custom", "endpoint": "http://localhost:5000/v1", "contextWindow": 8192, "supportsTools": true }
            """
        );
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "small" } ] }""")
        );
        bool storeRead = false;

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "custom",
            ModelCatalog.Load(userFile),
            handler,
            namedKeySource: _ =>
            {
                storeRead = true;
                return "stored-openai-key";
            },
            environment: name => name == "OPENAI_API_KEY" ? "env-key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        AssertNoCredential(handler.Requests[0]);
        Assert.False(storeRead);
    }

    [Theory]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("https://api.openai.com/v1/proxy")]
    public async Task A_declared_preset_endpoint_uses_the_preset_key(string endpoint)
    {
        using var temp = new TempDirectory();
        string userFile = WriteCatalog(
            temp,
            $$"""
            { "id": "own", "provider": "openai", "endpoint": "{{endpoint}}", "contextWindow": 8192, "supportsTools": true }
            """
        );
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "small" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "own",
            ModelCatalog.Load(userFile),
            handler,
            environment: name => name == "OPENAI_API_KEY" ? "sk-own" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal($"{endpoint}/models", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal(
            "Bearer sk-own",
            handler.Requests[0].Headers.GetValues("Authorization").Single()
        );
    }

    [Fact]
    public async Task Catalog_name_with_a_default_endpoint_uses_the_provider_default()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "gpt-4o-mini" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "gpt-4o-mini",
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://api.openai.com/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement model = draft.RootElement.GetProperty("models")[0];
        Assert.Equal("openai", model.GetProperty("provider").GetString());
        Assert.False(model.TryGetProperty("endpoint", out _));
    }

    [Fact]
    public async Task Preset_target_uses_its_base_url_and_key_and_drafts_qualified_ids()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(
                HttpStatusCode.OK,
                """{ "data": [ { "id": "llama-3.3-70b" }, { "id": "groq/compound" } ] }"""
            )
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "groq",
            Catalog(temp),
            handler,
            environment: name => name == "GROQ_API_KEY" ? "gsk-test" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://api.groq.com/openai/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        Assert.Equal(
            "Bearer gsk-test",
            handler.Requests[0].Headers.GetValues("Authorization").Single()
        );

        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        Assert.Equal(2, draft.RootElement.GetProperty("schemaVersion").GetInt32());
        JsonElement models = draft.RootElement.GetProperty("models");
        Assert.Equal(["groq/llama-3.3-70b", "groq/groq/compound"], Ids(models));
        Assert.Equal("groq", models[0].GetProperty("provider").GetString());
        Assert.Equal("llama-3.3-70b", models[0].GetProperty("wireId").GetString());
        Assert.False(models[0].TryGetProperty("endpoint", out _));
        Assert.False(models[0].TryGetProperty("auth", out _));
    }

    [Fact]
    public async Task Preset_target_falls_back_to_the_stored_provider_key()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "llama-3.3-70b" } ] }""")
        );
        string? requested = null;

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "groq",
            Catalog(temp),
            handler,
            namedKeySource: name =>
            {
                requested = name;
                return "stored-groq-key";
            },
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("groq", requested);
        Assert.Equal(
            "Bearer stored-groq-key",
            handler.Requests[0].Headers.GetValues("Authorization").Single()
        );
    }

    [Fact]
    public async Task Preset_target_accepts_the_google_key_alias()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "gemini-3-pro" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "google",
            Catalog(temp),
            handler,
            environment: name => name == "GEMINI_API_KEY" ? "gemini-key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/openai/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        Assert.Equal(
            "Bearer gemini-key",
            handler.Requests[0].Headers.GetValues("Authorization").Single()
        );
    }

    [Fact]
    public async Task Native_preset_target_keeps_bare_ids()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "gpt-4o" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "openai",
            Catalog(temp),
            handler,
            environment: name => name == "OPENAI_API_KEY" ? "sk-test" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://api.openai.com/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement model = draft.RootElement.GetProperty("models")[0];
        Assert.Equal("gpt-4o", model.GetProperty("id").GetString());
        Assert.False(model.TryGetProperty("wireId", out _));
    }

    [Fact]
    public async Task A_preset_catalog_model_uses_the_preset_and_drafts_qualified_ids()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "allam-2-7b" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "groq/allam-2-7b",
            Catalog(temp),
            handler,
            environment: name => name == "GROQ_API_KEY" ? "gsk-test" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://api.groq.com/openai/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement model = draft.RootElement.GetProperty("models")[0];
        Assert.Equal("groq/allam-2-7b", model.GetProperty("id").GetString());
        Assert.Equal("groq", model.GetProperty("provider").GetString());
        Assert.Equal("allam-2-7b", model.GetProperty("wireId").GetString());
    }

    [Fact]
    public async Task Auth_reference_sends_the_named_key()
    {
        using var temp = new TempDirectory();
        string userFile = WriteCatalog(
            temp,
            """
            { "id": "work", "provider": "openai", "endpoint": "https://example.test/v1", "contextWindow": 1, "supportsTools": true, "auth": "work" }
            """
        );
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "w" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "work",
            ModelCatalog.Load(userFile),
            handler,
            namedKeySource: name => name == "work" ? "work-key" : null,
            environment: name => name == "OPENAI_API_KEY" ? "preset-key" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "Bearer work-key",
            handler.Requests[0].Headers.GetValues("Authorization").Single()
        );
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        Assert.Equal(
            "work",
            draft.RootElement.GetProperty("models")[0].GetProperty("auth").GetString()
        );
    }

    [Fact]
    public async Task A_missing_auth_reference_names_the_key()
    {
        using var temp = new TempDirectory();
        string userFile = WriteCatalog(
            temp,
            """
            { "id": "work", "provider": "openai", "endpoint": "https://example.test/v1", "contextWindow": 1, "supportsTools": true, "auth": "missing" }
            """
        );
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, """{ "data": [] }"""));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "work",
            ModelCatalog.Load(userFile),
            handler,
            namedKeySource: _ => null,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains("missing", result.Error, StringComparison.Ordinal);
        Assert.Contains("auth.json", result.Error, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Missing_data_is_a_clear_error()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, """{ "models": [] }"""));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            UrlTarget,
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains(UrlTarget, result.Error, StringComparison.Ordinal);
        Assert.Contains("data", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_success_status_names_the_target_and_status()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.ServiceUnavailable, """{ "error": "down" }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            UrlTarget,
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains(UrlTarget, result.Error, StringComparison.Ordinal);
        Assert.Contains("503", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "401")]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    public async Task A_rejection_without_a_credential_explains_the_opt_in(
        HttpStatusCode status,
        string code
    )
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ => Json(status, """{ "error": "no key" }"""));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            UrlTarget,
            Catalog(temp),
            handler,
            environment: name => name == "OPENAI_API_KEY" ? "sk-env-secret" : null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains($"{UrlTarget}/models", result.Error, StringComparison.Ordinal);
        Assert.Contains(code, result.Error, StringComparison.Ordinal);
        Assert.Contains("No key was sent", result.Error, StringComparison.Ordinal);
        Assert.Contains("not the provider's own", result.Error, StringComparison.Ordinal);
        Assert.Contains("'auth' reference", result.Error, StringComparison.Ordinal);
        Assert.Equal(2, result.Error!.Split("No key was sent").Length);
        Assert.DoesNotContain("sk-env-secret", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_json_is_a_clear_error()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, "not json at all"));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            UrlTarget,
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains(UrlTarget, result.Error, StringComparison.Ordinal);
        Assert.Contains("JSON", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ambiguous_bare_model_names_its_candidates_and_makes_no_request()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, """{ "data": [] }"""));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "deepseek-v4-flash",
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains("deepseek-v4-flash", result.Error, StringComparison.Ordinal);
        Assert.Contains("deepseek/deepseek-v4-flash", result.Error, StringComparison.Ordinal);
        Assert.Contains("opencode/deepseek-v4-flash", result.Error, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_unknown_name_names_the_target_and_points_at_the_catalog()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, """{ "data": [] }"""));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "nope",
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains("nope", result.Error, StringComparison.Ordinal);
        Assert.Contains("models.json", result.Error, StringComparison.Ordinal);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_custom_provider_target_uses_its_models_url_and_declared_placement()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(_ =>
            Json(HttpStatusCode.OK, """{ "data": [ { "id": "claude-wire" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "relay/claude-wire",
            CustomCatalog(temp, CredentialPlacement.XApiKey),
            handler,
            namedKeySource: id => id == "relay" ? "relay-key" : null,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://relay.example/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
        Assert.Equal("relay-key", handler.Requests[0].Headers.GetValues("x-api-key").Single());
        Assert.False(handler.Requests[0].Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task A_custom_provider_discovery_refuses_a_redirect_to_another_host()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(request =>
            string.Equals(
                request.RequestUri!.Host,
                "relay.example",
                StringComparison.OrdinalIgnoreCase
            )
                ? new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://elsewhere.example/v2/models") },
                }
                : Json(HttpStatusCode.OK, """{ "data": [] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "relay/claude-wire",
            CustomCatalog(temp, CredentialPlacement.XApiKey),
            handler,
            namedKeySource: id => id == "relay" ? "relay-key" : null,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains("elsewhere.example", result.Error, StringComparison.Ordinal);
        Assert.Contains("configured host", result.Error, StringComparison.Ordinal);
        // The refused redirect never went out: only the request to the configured host exists.
        Assert.Single(handler.Requests);
        Assert.Equal("relay.example", handler.Requests[0].RequestUri!.Host, ignoreCase: true);
    }

    [Fact]
    public async Task A_custom_provider_discovery_follows_a_redirect_inside_its_host()
    {
        using var temp = new TempDirectory();
        using var handler = new FakeHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/v1/models", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("https://relay.example/v2/models") },
                }
                : Json(HttpStatusCode.OK, """{ "data": [ { "id": "claude-wire" } ] }""")
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "relay/claude-wire",
            CustomCatalog(temp, CredentialPlacement.XApiKey),
            handler,
            namedKeySource: id => id == "relay" ? "relay-key" : null,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(
            "https://relay.example/v2/models",
            handler.Requests[1].RequestUri!.AbsoluteUri
        );
        Assert.Equal("relay-key", handler.Requests[1].Headers.GetValues("x-api-key").Single());
    }

    [Fact]
    [Trait("Discovery", "ProviderApi")]
    public async Task OpenCode_Go_discovery_maps_api_families_and_model_metadata()
    {
        using var temp = new TempDirectory();
        string response = File.ReadAllText(Fixture("opencode-go-models.json"));
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, response));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "opencode-go",
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        using JsonDocument draft = JsonDocument.Parse(result.Draft);
        JsonElement[] models = [.. draft.RootElement.GetProperty("models").EnumerateArray()];
        Assert.Equal(
            ["openai-chat", "openai-responses", "anthropic-messages", "google"],
            models.Select(model => model.GetProperty("apiFamily").GetString())
        );
        Assert.Equal(
            [
                "https://opencode.ai/inference/go/openai/v1",
                "https://opencode.ai/inference/go/openai/v1",
                "https://opencode.ai/inference/go/anthropic/v1",
                "https://opencode.ai/inference/go/google/v1beta",
            ],
            models.Select(model => model.GetProperty("endpoint").GetString())
        );

        JsonElement google = models[3];
        Assert.True(
            Guid.TryParse(
                handler.Requests[0].Headers.GetValues("x-opencode-session").Single(),
                out _
            )
        );
        Assert.Equal(1_048_576, google.GetProperty("contextWindow").GetInt32());
        Assert.Equal(65_536, google.GetProperty("maxOutput").GetInt32());
        Assert.True(google.GetProperty("supportsTools").GetBoolean());
        Assert.True(google.GetProperty("supportsVision").GetBoolean());
        Assert.Equal("effort", google.GetProperty("reasoning").GetString());
        JsonElement capabilities = google.GetProperty("capabilities");
        Assert.Contains(
            "image",
            capabilities
                .GetProperty("inputModalities")
                .EnumerateArray()
                .Select(item => item.GetString())
        );
        Assert.Contains(
            "reasoning-effort:high",
            capabilities.GetProperty("features").EnumerateArray().Select(item => item.GetString())
        );
    }

    [Fact]
    [Trait("Discovery", "ProviderApi")]
    public async Task OpenCode_Go_discovery_requests_the_preset_models_url_not_a_family_url()
    {
        using var temp = new TempDirectory();
        string response = File.ReadAllText(Fixture("opencode-go-models.json"));
        // The handler serves only the discovery endpoint the preset declares; a family URL's
        // /models path is a 404, which is exactly what the live endpoint answers with.
        using var handler = new FakeHandler(request =>
            request.RequestUri!.AbsoluteUri == "https://opencode.ai/inference/go/v1/models"
                ? Json(HttpStatusCode.OK, response)
                : new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "Not Found" }
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "opencode-go",
            Catalog(temp),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://opencode.ai/inference/go/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    [Trait("Discovery", "ProviderApi")]
    public async Task A_hand_entry_of_a_preset_model_discovers_through_the_preset_models_url()
    {
        using var temp = new TempDirectory();
        string response = File.ReadAllText(Fixture("opencode-go-models.json"));
        using var handler = new FakeHandler(request =>
            request.RequestUri!.AbsoluteUri == "https://opencode.ai/inference/go/v1/models"
                ? Json(HttpStatusCode.OK, response)
                : new HttpResponseMessage(HttpStatusCode.NotFound) { ReasonPhrase = "Not Found" }
        );
        string path = temp.File("models.json");
        File.WriteAllText(
            path,
            """{ "schemaVersion": 2, "models": [ { "id": "go/beam", "provider": "opencode-go", "contextWindow": 8192, "supportsTools": true, "apiFamily": "anthropic-messages" } ] }"""
        );

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "go/beam",
            ModelCatalog.Load(path),
            handler,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(
            "https://opencode.ai/inference/go/v1/models",
            handler.Requests[0].RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    [Trait("Discovery", "ProviderApi")]
    public async Task Custom_provider_discovery_rejects_a_model_base_url_on_another_host()
    {
        using var temp = new TempDirectory();
        string response = File.ReadAllText(Fixture("custom-discovery-cross-host.json"));
        using var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, response));

        DiscoverResult result = await DiscoverCommand.RunAsync(
            "relay/claude-wire",
            CustomCatalog(temp, CredentialPlacement.XApiKey),
            handler,
            namedKeySource: id => id == "relay" ? "relay-key" : null,
            environment: _ => null,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Succeeded);
        Assert.Contains("elsewhere.example", result.Error, StringComparison.Ordinal);
        Assert.Contains("configured host", result.Error, StringComparison.Ordinal);
    }

    private static ModelCatalog CustomCatalog(TempDirectory temp, CredentialPlacement placement)
    {
        string settingsPath = temp.File("settings.json");
        var provider = new CustomProvider(
            "relay",
            new Uri("https://relay.example/v1"),
            "claude-wire",
            "relay/claude-wire",
            ProviderApiFamily.AnthropicMessages,
            placement,
            new Uri("https://relay.example/v1/models")
        );
        SettingsStore.WriteProvider(provider, settingsPath);

        AgentSettings settings = SettingsStore.Resolve(settingsPath, _ => null);
        return HarnessFactory.LoadCatalog(settings, temp.File("models.json"));
    }

    private static ModelCatalog Catalog(TempDirectory temp) =>
        ModelCatalog.Load(temp.File("models.json"));

    private static string Fixture(string name) =>
        Path.Combine(RepositoryRoot.Find(), "tests", "fixtures", "discovery", name);

    private static string WriteCatalog(TempDirectory temp, string model)
    {
        string path = temp.File("models.json");
        File.WriteAllText(path, $$"""{ "schemaVersion": 1, "models": [ {{model}} ] }""");
        return path;
    }

    private static string[] Ids(JsonElement models) =>
        [.. models.EnumerateArray().Select(model => model.GetProperty("id").GetString()!)];

    private static void AssertNoCredential(HttpRequestMessage request)
    {
        Assert.False(request.Headers.Contains("Authorization"));
        Assert.False(request.Headers.Contains("x-api-key"));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }
}
