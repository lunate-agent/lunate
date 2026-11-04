using System.Net;
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
    public void A_no_op_run_writes_nothing_and_names_every_step_skipped()
    {
        (int exit, string output, string errors) = Run(["setup"], stdinIsTerminal: false);

        Assert.Equal(0, exit);
        Assert.Contains(
            "written: nothing — every step was skipped",
            output,
            StringComparison.Ordinal
        );
        Assert.False(File.Exists(SettingsPath), "a no-op run rewrites nothing");
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

    private (int Exit, string Output, string Errors) Run(
        string[] args,
        bool stdinIsTerminal,
        string? input = null,
        HttpMessageHandler? discovery = null,
        Func<string, string?>? environment = null
    )
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var factory = new FakeChatClientFactory(
            new ScriptedChatClient().Enqueue(Scripts.Text("OK"))
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

    private static HttpMessageHandler Discovery() =>
        new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"data":[{"id":"deep/deepseek-chat"}]}""",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                )
        );
}
