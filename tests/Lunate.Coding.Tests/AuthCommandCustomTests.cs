using Lunate.Ai;

namespace Lunate.Coding.Tests;

public sealed class AuthCommandCustomTests
{
    private const string Secret = "sk-custom-endpoint-secret";
    private const string BaseUrl = "http://127.0.0.1:11434/v1";
    private const string Model = "qwen2.5-coder";

    [Fact]
    public void Custom_login_prompts_for_the_fields_then_the_masked_key()
    {
        using var temp = new TempDirectory();
        var prompt = new ScriptedSecretPrompt(Secret);
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: true,
            prompt: prompt,
            input: new StringReader($"local\n{BaseUrl}\n{Model}\n")
        );

        (int exit, string output, string errors) = Run(["login", "--custom"], seams);

        Assert.Equal(0, exit);
        Assert.Equal(Secret, AuthStore.Load(seams.AuthPath, _ => null).Keys["local"]);
        AgentSettings settings = SettingsStore.Resolve(seams.SettingsPath, _ => null);
        CustomProvider stored = Assert.Single(settings.Providers);
        Assert.Equal("local", stored.Id);
        Assert.Equal(new Uri(BaseUrl), stored.BaseUrl);
        Assert.Equal(Model, stored.Model);
        Assert.Equal($"local/{Model}", stored.CatalogName);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
        Assert.Equal(["API key for local"], prompt.Prompts);
        Assert.Contains("Provider id", output, StringComparison.Ordinal);
        Assert.Contains("Base URL", output, StringComparison.Ordinal);
        Assert.Contains("Model id", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_login_refuses_a_non_loopback_http_url_without_echoing_it()
    {
        using var temp = new TempDirectory();
        var prompt = new ScriptedSecretPrompt(Secret);
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: true,
            prompt: prompt,
            input: new StringReader("local\nhttp://example.com/v1\nqwen\n")
        );

        (int exit, string output, string errors) = Run(["login", "--custom"], seams);

        Assert.Equal(2, exit);
        Assert.Contains("http", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", output + errors, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Fact]
    public void Custom_login_refuses_a_missing_model_id_without_echoing_the_key()
    {
        using var temp = new TempDirectory();
        var prompt = new ScriptedSecretPrompt(Secret);
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: true,
            prompt: prompt,
            input: new StringReader("local\nhttps://example.com/v1\n\n")
        );

        (int exit, string output, string errors) = Run(["login", "--custom"], seams);

        Assert.Equal(2, exit);
        Assert.Contains("model id", errors, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", output + errors, StringComparison.Ordinal);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("https://example.com/v1")]
    public void Custom_login_accepts_https_and_loopback_http(string baseUrl)
    {
        using var temp = new TempDirectory();
        var prompt = new ScriptedSecretPrompt(Secret);
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: true,
            prompt: prompt,
            input: new StringReader($"local\n{baseUrl}\n{Model}\n")
        );

        (int exit, string output, string errors) = Run(["login", "--custom"], seams);

        Assert.Equal(0, exit);
        Assert.Equal(
            new Uri(baseUrl),
            Assert.Single(SettingsStore.Resolve(seams.SettingsPath, _ => null).Providers).BaseUrl
        );
        Assert.Equal(Secret, AuthStore.Load(seams.AuthPath, _ => null).Keys["local"]);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_login_with_key_stdin_and_flags_stores_without_a_terminal()
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, string output, string errors) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "local",
                "--base-url",
                BaseUrl,
                "--model",
                Model,
            ],
            seams
        );

        Assert.Equal(0, exit);
        Assert.Equal(Secret, AuthStore.Load(seams.AuthPath, _ => null).Keys["local"]);
        Assert.Equal(
            $"local/{Model}",
            Assert
                .Single(SettingsStore.Resolve(seams.SettingsPath, _ => null).Providers)
                .CatalogName
        );
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_login_writes_the_declared_family_placement_and_models_url()
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, string output, string errors) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "relay",
                "--base-url",
                "https://relay.example/v1",
                "--model",
                "claude-wire",
                "--api-family",
                "anthropic-messages",
                "--credential-placement",
                "x-api-key",
                "--models-url",
                "https://relay.example/v1/models",
            ],
            seams
        );

        Assert.Equal(0, exit);
        CustomProvider stored = Assert.Single(
            SettingsStore.Resolve(seams.SettingsPath, _ => null).Providers
        );
        Assert.Equal(ProviderApiFamily.AnthropicMessages, stored.ApiFamily);
        Assert.Equal(CredentialPlacement.XApiKey, stored.CredentialPlacement);
        Assert.Equal(new Uri("https://relay.example/v1/models"), stored.ModelsUrl);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [Fact]
    public void An_omitted_family_defaults_to_openai_chat_with_a_printed_note()
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, string output, string errors) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "local",
                "--base-url",
                BaseUrl,
                "--model",
                Model,
            ],
            seams
        );

        Assert.Equal(0, exit);
        Assert.Equal(
            ProviderApiFamily.OpenAiChat,
            Assert.Single(SettingsStore.Resolve(seams.SettingsPath, _ => null).Providers).ApiFamily
        );
        Assert.Contains("--api-family", output, StringComparison.Ordinal);
        Assert.Contains("openai-chat", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_login_refuses_an_unknown_family_by_name_without_writing_anything()
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, string output, string errors) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "relay",
                "--base-url",
                "https://relay.example/v1",
                "--model",
                "m",
                "--api-family",
                "openai-realtime",
            ],
            seams
        );

        Assert.Equal(2, exit);
        Assert.Contains("openai-realtime", errors, StringComparison.Ordinal);
        Assert.Contains("supported families", errors, StringComparison.Ordinal);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Fact]
    public void Custom_login_refuses_an_unusable_placement_by_name()
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, string output, string errors) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "relay",
                "--base-url",
                "https://relay.example/v1",
                "--model",
                "m",
                "--credential-placement",
                "bearer ",
            ],
            seams
        );

        Assert.Equal(2, exit);
        Assert.Contains("not a placement", errors, StringComparison.Ordinal);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Fact]
    public void Custom_login_refuses_a_models_url_outside_the_endpoint_rule()
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, string output, string errors) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "relay",
                "--base-url",
                "https://relay.example/v1",
                "--model",
                "m",
                "--models-url",
                "http://insecure.example/v1/models",
            ],
            seams
        );

        Assert.Equal(2, exit);
        Assert.Contains("http", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("insecure.example", output + errors, StringComparison.Ordinal);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Theory]
    [InlineData("--api-family")]
    [InlineData("--credential-placement")]
    [InlineData("--models-url")]
    public void A_new_flag_without_a_value_is_a_usage_error(string flag)
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, _, _) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "local",
                "--base-url",
                BaseUrl,
                "--model",
                Model,
                flag,
            ],
            seams
        );

        Assert.Equal(2, exit);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Theory]
    [InlineData("--api-family")]
    [InlineData("--credential-placement")]
    [InlineData("--models-url")]
    public void A_repeated_new_flag_is_a_usage_error(string flag)
    {
        using var temp = new TempDirectory();
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            input: new StringReader(Secret + "\n")
        );

        (int exit, _, _) = Run(
            [
                "login",
                "--custom",
                "--key-stdin",
                "--id",
                "local",
                "--base-url",
                BaseUrl,
                "--model",
                Model,
                flag,
                "openai-chat",
                flag,
                "anthropic-messages",
            ],
            seams
        );

        Assert.Equal(2, exit);
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Fact]
    public void Custom_login_with_key_stdin_missing_a_flag_exits_two_without_reading_the_key()
    {
        using var temp = new TempDirectory();
        var input = new StringReader(Secret + "\n");
        AuthCommandSeams seams = Seams(temp, stdinIsTerminal: false, input: input);

        (int exit, string output, string errors) = Run(
            ["login", "--custom", "--key-stdin", "--id", "local"],
            seams
        );

        Assert.Equal(2, exit);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
        Assert.Equal(Secret, input.ReadLine());
        Assert.False(File.Exists(seams.AuthPath));
        Assert.False(File.Exists(seams.SettingsPath));
    }

    [Fact]
    public void Custom_login_on_a_pipe_without_key_stdin_exits_two_instead_of_hanging()
    {
        using var temp = new TempDirectory();
        var prompt = new ScriptedSecretPrompt(Secret);
        AuthCommandSeams seams = Seams(
            temp,
            stdinIsTerminal: false,
            prompt: prompt,
            input: new StringReader(string.Empty)
        );

        (int exit, _, string errors) = Run(["login", "--custom"], seams);

        Assert.Equal(2, exit);
        Assert.Contains("--key-stdin", errors, StringComparison.Ordinal);
        Assert.Empty(prompt.Prompts);
        Assert.False(File.Exists(seams.AuthPath));
    }

    [Fact]
    public void Status_resolves_a_stored_custom_provider()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "model": "local/qwen2.5-coder",
              "providers": {
                "local": {
                  "baseUrl": "https://example.com/v1",
                  "model": "qwen2.5-coder",
                  "catalogName": "local/qwen2.5-coder"
                }
              }
            }
            """
        );
        StoreKey(temp, "local", Secret);

        (int exit, string output, string errors) = Run(["status"], Seams(temp));

        Assert.Equal(0, exit);
        Assert.Contains("default provider: openai", output, StringComparison.Ordinal);
        Assert.Contains("credential: stored", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, output + errors, StringComparison.Ordinal);
    }

    private static (int Exit, string Output, string Errors) Run(
        string[] args,
        AuthCommandSeams seams
    )
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        int exit = AuthCommand.Run(args, output, errors, seams);
        return (exit, output.ToString(), errors.ToString());
    }

    private static AuthCommandSeams Seams(
        TempDirectory temp,
        bool stdinIsTerminal = false,
        ISecretPrompt? prompt = null,
        TextReader? input = null
    ) =>
        new()
        {
            AuthPath = temp.File("auth.json"),
            SettingsPath = temp.File("settings.json"),
            ModelsPath = temp.File("models.json"),
            Environment = _ => null,
            SecretPrompt = prompt,
            StdinIsTerminal = stdinIsTerminal,
            Input = input,
        };

    private static void StoreKey(TempDirectory temp, string name, string secret)
    {
        AuthStore store = AuthStore.Load(temp.File("auth.json"), _ => null);
        store.Set(name, secret);
        store.Save();
    }

    private sealed class ScriptedSecretPrompt(params string?[] values) : ISecretPrompt
    {
        private readonly Queue<string?> _values = new(values);

        public List<string> Prompts { get; } = [];

        public Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return Task.FromResult(_values.Count > 0 ? _values.Dequeue() : null);
        }
    }
}
