using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>lunate setup</c> flag path (spec: Setup command line): a CI run without a terminal
/// writes the full configuration through the store writers and prints no secret; an answer that
/// needs interaction exits 2 and names the credential; absent flags skip their steps and invent
/// nothing; --force replaces an existing entry; --thinking is valid only for a model that
/// declares a reasoning style; --as-decision binds decisions.profile instead of the chat usage.
/// </summary>
[Collection("setup-command")]
public sealed class SetupCommandTests : IDisposable
{
    private const string Secret = "sk-setup-command-please-never-echo";

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string SettingsPath => AuthStore.SettingsFilePath(_temp.File("auth.json"));

    private string ProviderStorePath => AuthStore.ProviderStoreFilePath(_temp.File("auth.json"));

    [PosixFact]
    public void A_ci_run_writes_the_stores_without_a_tty_and_never_prints_the_secret()
    {
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "openrouter",
                "--model",
                "deep/deepseek-chat",
                "--profile",
                "main",
                "--key-stdin",
            ],
            stdinIsTerminal: false,
            input: Secret + "\n",
            discovery: Discovery()
        );

        Assert.Equal(0, exit);
        Assert.Contains("connection test: passed", output, StringComparison.Ordinal);
        Assert.Contains("model check: passed", output, StringComparison.Ordinal);
        Assert.Contains(
            "written: profile 'main' (model deep/deepseek-chat)",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains("written: usages.chat → main", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);

        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        Assert.Equal(
            "auth:openrouter",
            store
                .RootElement.GetProperty("providers")[0]
                .GetProperty("credentialReference")
                .GetString()
        );
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "deep/deepseek-chat",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("main")
                .GetProperty("model")
                .GetString()
        );
        Assert.Equal(
            "main",
            settings.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
        Assert.Contains(
            Secret,
            File.ReadAllText(_temp.File("auth.json")),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            Secret,
            store.RootElement.ToString() + settings.RootElement.ToString(),
            StringComparison.Ordinal
        );
    }

    [PosixFact]
    public void A_run_without_a_credential_refuses_and_names_the_missing_answer()
    {
        (int exit, string output, string errors) = Run(
            ["setup", "--provider", "openai", "--no-test"],
            stdinIsTerminal: false
        );

        Assert.Equal(2, exit);
        Assert.Contains("credential", errors, StringComparison.Ordinal);
        Assert.Contains("--key-stdin", errors, StringComparison.Ordinal);
        Assert.False(File.Exists(ProviderStorePath), "a refused run writes nothing");
        Assert.False(File.Exists(SettingsPath), "a refused run writes nothing");
    }

    [PosixFact]
    public void A_no_flag_run_without_a_terminal_exits_2_and_names_the_missing_answers()
    {
        (int exit, string output, string errors) = Run(["setup"], stdinIsTerminal: false);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("interactive setup wizard", errors, StringComparison.Ordinal);
        Assert.Contains("--provider", errors, StringComparison.Ordinal);
        Assert.Contains("--credential", errors, StringComparison.Ordinal);
        Assert.Contains("--model", errors, StringComparison.Ordinal);
        Assert.False(File.Exists(SettingsPath), "a refused run writes nothing");
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void The_providers_standard_variable_is_the_offered_reference()
    {
        (int exit, string output, string errors) = Run(
            ["setup", "--provider", "openai", "--no-test", "--no-model-check"],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "env-var-key-not-echoed" : null
        );

        Assert.Equal(0, exit);
        Assert.Contains("credential: env:OPENAI_API_KEY", output, StringComparison.Ordinal);
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        Assert.Equal(
            "env:OPENAI_API_KEY",
            store
                .RootElement.GetProperty("providers")[0]
                .GetProperty("credentialReference")
                .GetString()
        );
        string settingsFile = SettingsPath;
        Assert.False(
            File.Exists(settingsFile),
            "a provider-and-credential-only run writes no settings file"
        );
        Assert.DoesNotContain(
            "env-var-key-not-echoed",
            output + errors + File.ReadAllText(ProviderStorePath),
            StringComparison.Ordinal
        );
        Assert.False(
            File.Exists(_temp.File("auth.json")),
            "an environment reference stores no key"
        );
    }

    [PosixFact]
    public void An_existing_entry_refuses_until_force()
    {
        (int first, _, _) = Run(
            ["setup", "--provider", "openai", "--no-test", "--no-model-check"],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "first-key-not-echoed" : null
        );
        Assert.Equal(0, first);

        (int refused, _, string errors) = Run(
            ["setup", "--provider", "openai", "--no-test", "--no-model-check"],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "second-key-not-echoed" : null
        );
        Assert.Equal(2, refused);
        Assert.Contains("--force", errors, StringComparison.Ordinal);

        (int replaced, string replacedOutput, string replacedErrors) = Run(
            ["setup", "--provider", "openai", "--no-test", "--no-model-check", "--force"],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "third-key-not-echoed" : null
        );
        Assert.Equal(0, replaced);
        Assert.Contains(
            "written: provider entry 'openai'",
            replacedOutput,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(Secret, replacedOutput + replacedErrors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void A_thinking_level_is_written_when_the_model_declares_effort()
    {
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "openai",
                "--model",
                "gpt-5",
                "--profile",
                "main",
                "--thinking",
                "low",
                "--no-test",
                "--no-model-check",
            ],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "walk-key-not-echoed" : null
        );

        Assert.Equal(0, exit);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "low",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("main")
                .GetProperty("reasoningLevel")
                .GetString()
        );
        Assert.Contains(
            "written: profile 'main' (model gpt-5, thinking low)",
            output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void A_thinking_level_refuses_a_model_without_reasoning()
    {
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "openai",
                "--model",
                "gpt-3.5-turbo",
                "--profile",
                "main",
                "--thinking",
                "high",
                "--no-test",
                "--no-model-check",
            ],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "walk-key-not-echoed" : null
        );

        Assert.Equal(2, exit);
        Assert.Contains("declares no reasoning", errors, StringComparison.Ordinal);
        Assert.False(File.Exists(SettingsPath), "a refused run writes nothing");
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void A_custom_endpoints_bare_model_id_ambiguous_in_the_catalog_completes_the_run()
    {
        // #675 on the flag path: the id is three catalog entries' wire id, and the walked provider
        // (a custom endpoint) owns none of them. The run must not fail with an ambiguity message.
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "local",
                "--endpoint",
                "https://local.example.test/v1",
                "--model",
                "kimi-k2.7-code",
                "--profile",
                "main",
                "--key-stdin",
            ],
            stdinIsTerminal: false,
            input: Secret + "\n",
            discovery: Discovery("""{"data":[{"id":"kimi-k2.7-code"}]}""")
        );

        Assert.Equal(0, exit);
        Assert.DoesNotContain("ambiguous", output + errors, StringComparison.Ordinal);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "kimi-k2.7-code",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("main")
                .GetProperty("model")
                .GetString()
        );
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void As_decision_binds_decisions_profile_instead_of_the_chat_usage()
    {
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "openai",
                "--model",
                "gpt-5",
                "--profile",
                "main",
                "--as-decision",
                "--no-test",
                "--no-model-check",
            ],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "walk-key-not-echoed" : null
        );

        Assert.Equal(0, exit);
        Assert.Contains("written: decisions.profile → main", output, StringComparison.Ordinal);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "main",
            settings.RootElement.GetProperty("decisions").GetProperty("profile").GetString()
        );
        Assert.False(
            settings.RootElement.TryGetProperty("usages", out JsonElement usages)
                && usages.TryGetProperty("chat", out _),
            "--as-decision binds the profile in place of the chat usage"
        );
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void A_systemone_model_is_refused_for_the_chat_profile_with_the_shared_words()
    {
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "opencode-go",
                "--model",
                "opencode-go/jev-1.13",
                "--profile",
                "main",
                "--no-test",
                "--no-model-check",
            ],
            stdinIsTerminal: false,
            environment: name => name == "OPENCODE_GO_API_KEY" ? "systemone-key" : null
        );

        Assert.Equal(2, exit);
        Assert.Contains("decision-only", errors, StringComparison.Ordinal);
        Assert.Contains("lunate setup", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("; Usage:", errors, StringComparison.Ordinal);
        Assert.False(File.Exists(SettingsPath), "a refused binding writes no settings");
        Assert.False(File.Exists(ProviderStorePath), "a refused binding writes no provider");
        Assert.DoesNotContain("systemone-key", output + errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void Api_family_overrides_the_presets_default_for_the_check_and_the_writes()
    {
        // S-12: the flag used to be validated and then ignored; the check, the entry and the
        // profile must all use the family it named.
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var factory = new FakeChatClientFactory(
            new ScriptedChatClient().Enqueue(Scripts.Text("OK"))
        );
        int exit = Cli.Run(
            [
                "setup",
                "--provider",
                "opencode-go",
                "--api-family",
                "anthropic-messages",
                "--model",
                "anthropic/claude-sonnet-5-5",
                "--profile",
                "main",
            ],
            output,
            errors,
            TestContext.Current.CancellationToken,
            stdinIsTerminal: false,
            setupSeams: new SetupCommandSeams
            {
                AuthPath = _temp.File("auth.json"),
                Environment = name => name == "OPENCODE_API_KEY" ? "family-key-not-echoed" : null,
                DiscoveryTransport = Discovery(),
                ChatFactory = factory,
            }
        );

        Assert.Equal(0, exit);
        Assert.Equal("anthropic-messages", factory.CreatedModel?.ApiFamily);
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        JsonElement entry = store.RootElement.GetProperty("providers")[0];
        Assert.Equal(
            "https://opencode.ai/inference/go/anthropic/v1",
            entry.GetProperty("baseUrls").GetProperty("anthropic-messages").GetString()
        );
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "anthropic-messages",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("main")
                .GetProperty("apiFamily")
                .GetString()
        );
        Assert.DoesNotContain("family-key-not-echoed", output.ToString(), StringComparison.Ordinal);
    }

    [PosixFact]
    public void Api_family_outside_the_presets_families_is_refused_by_name()
    {
        (int exit, string _, string errors) = Run(
            [
                "setup",
                "--provider",
                "openai",
                "--api-family",
                "anthropic-messages",
                "--model",
                "gpt-5",
                "--no-test",
                "--no-model-check",
            ],
            stdinIsTerminal: false,
            environment: name => name == "OPENAI_API_KEY" ? "refused-family-key" : null
        );

        Assert.Equal(2, exit);
        Assert.Contains(
            "provider 'openai' does not declare the API family 'anthropic-messages'",
            errors,
            StringComparison.Ordinal
        );
        Assert.False(File.Exists(ProviderStorePath), "a refused family writes nothing");
    }

    [PosixFact]
    public void No_test_sends_no_provider_request()
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        // The check would create a client and ask it for a completion; the discovery transport is
        // what the connection test would use. Neither may be touched by an offline run.
        var factory = new FakeChatClientFactory(new ScriptedChatClient());
        var discovery = new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"data":[{"id":"gpt-5"}]}""",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                )
        );

        int exit = Cli.Run(
            ["setup", "--provider", "openai", "--model", "gpt-5", "--profile", "main", "--no-test"],
            output,
            errors,
            TestContext.Current.CancellationToken,
            stdinIsTerminal: false,
            setupSeams: new SetupCommandSeams
            {
                AuthPath = _temp.File("auth.json"),
                Environment = name => name == "OPENAI_API_KEY" ? "no-test-key-not-echoed" : null,
                DiscoveryTransport = discovery,
                ChatFactory = factory,
            }
        );

        Assert.Equal(0, exit);
        Assert.Empty(discovery.Requests);
        Assert.Null(factory.CreatedModel);
        string summary = output.ToString();
        Assert.Contains("connection test: skipped", summary, StringComparison.Ordinal);
        Assert.Contains(
            "model check: skipped — the model stays unverified",
            summary,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("connection test: passed", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("model check: passed", summary, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, summary + errors.ToString(), StringComparison.Ordinal);
    }

    [PosixFact]
    public void An_unknown_thinking_level_is_refused_with_the_values_the_usage_line_lists()
    {
        (int exit, string _, string errors) = Run(
            [
                "setup",
                "--provider",
                "openrouter",
                "--model",
                "deep/deepseek-chat",
                "--profile",
                "main",
                "--credential",
                "env:OPENROUTER_API_KEY",
                "--thinking",
                "loud",
                "--no-test",
            ],
            stdinIsTerminal: false
        );

        // `off` is an accepted level (it writes no level), so the usage line the refusal prints
        // has to list it alongside the three levels it already named (#585 N-5).
        Assert.Equal(2, exit);
        Assert.Contains("use off, low, medium or high", errors, StringComparison.Ordinal);
        Assert.Contains("--thinking <off|low|medium|high>", errors, StringComparison.Ordinal);
    }

    [PosixFact]
    public void A_failed_model_check_names_the_model_the_family_and_the_status()
    {
        (int exit, string output, string errors) = Run(
            [
                "setup",
                "--provider",
                "local",
                "--endpoint",
                "https://local.example.test/v1",
                "--api-family",
                "anthropic-messages",
                "--credential",
                "env:LOCAL_API_KEY",
                "--model",
                "claude-test",
                "--profile",
                "main",
            ],
            stdinIsTerminal: false,
            discovery: Discovery(),
            environment: name => name == "LOCAL_API_KEY" ? Secret : null,
            client: new RejectingChatClient()
        );

        // The failure detail is the one record both entry points print, so a run that trusted the
        // summary could not tell a wrong model from a wrong family or from an unreadable answer
        // without the three facts in it (#585 N-5).
        Assert.Equal(0, exit);
        Assert.Contains(
            "model check: failed (claude-test on family anthropic-messages: HTTP 400 Bad Request) "
                + "— the model stays unverified",
            output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    private (int Exit, string Output, string Errors) Run(
        string[] args,
        bool stdinIsTerminal,
        string? input = null,
        HttpMessageHandler? discovery = null,
        Func<string, string?>? environment = null,
        IChatClient? client = null
    )
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var factory = new FakeChatClientFactory(
            client ?? new ScriptedChatClient().Enqueue(Scripts.Text("OK"))
        );
        int exit = Cli.Run(
            args,
            output,
            errors,
            TestContext.Current.CancellationToken,
            stdinIsTerminal: stdinIsTerminal,
            setupSeams: new SetupCommandSeams
            {
                AuthPath = _temp.File("auth.json"),
                Environment = environment ?? (_ => null),
                Input = input is null ? null : new StringReader(input),
                DiscoveryTransport = discovery,
                ChatFactory = factory,
            }
        );
        return (exit, output.ToString(), errors.ToString());
    }

    private static HttpMessageHandler Discovery(
        string body = """{"data":[{"id":"deep/deepseek-chat"}]}"""
    ) =>
        new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    }
                )
        );

    /// <summary>A chat client the provider rejects: the wrong-model or wrong-family case.</summary>
    private sealed class RejectingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Task.FromException<ChatResponse>(new HttpRequestException("HTTP 400 Bad Request"));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
