using System.Runtime.InteropServices;
using System.Text.Json;

namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>lunate doctor [--json]</c> report: one offline view of the facts a bug report needs, a
/// credential reported only as present/absent, required-path health, and the documented JSON shape.
/// Each case supplies its own state directory, environment, terminal facts, process path and
/// update-check cache, so no test depends on the machine running it.
/// </summary>
public sealed class DoctorCommandTests
{
    private const string Secret = "sk-doctor-secret";
    private const string Settings = """{"schemaVersion":2,"model":"gpt-4.1"}""";
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_human_report_prints_every_fact_and_exits_zero_when_the_paths_exist()
    {
        using var fixture = new Fixture();

        Result result = fixture.Run();

        Assert.Equal(DoctorCommand.ExitOk, result.ExitCode);
        Assert.Empty(result.Errors);
        string output = result.Output;
        Assert.Contains("version: ", output, StringComparison.Ordinal);
        Assert.Contains("os: macOS 15.0", output, StringComparison.Ordinal);
        Assert.Contains("architecture: Arm64", output, StringComparison.Ordinal);
        Assert.Contains("install channel: self-contained", output, StringComparison.Ordinal);
        Assert.Contains(
            $"config: {fixture.SettingsPath} (present)",
            output,
            StringComparison.Ordinal
        );
        Assert.Contains($"state: {fixture.Root} (present)", output, StringComparison.Ordinal);
        Assert.Contains("provider: openai", output, StringComparison.Ordinal);
        Assert.Contains("model: gpt-4.1", output, StringComparison.Ordinal);
        Assert.Contains("credential: present (stored)", output, StringComparison.Ordinal);
        Assert.Contains("docker present, podman absent", output, StringComparison.Ordinal);
        Assert.Contains("update check: latest 0.6.1", output, StringComparison.Ordinal);
        Assert.Contains("health: healthy", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_seeded_key_never_appears_in_the_text_or_json_output()
    {
        using var fixture = new Fixture();

        Result text = fixture.Run();
        Result json = fixture.Run("--json");

        Assert.DoesNotContain(Secret, text.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text.Errors, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, json.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, json.Errors, StringComparison.Ordinal);
        Assert.Contains("credential: present", text.Output, StringComparison.Ordinal);
        Assert.False(
            File.Exists(Path.Combine(fixture.Root, "providers.json")),
            "doctor must not migrate providers.json"
        );
        using JsonDocument document = JsonDocument.Parse(json.Output);
        Assert.Equal(
            "present",
            document.RootElement.GetProperty("credential").GetProperty("state").GetString()
        );
    }

    [Fact]
    public void Missing_required_paths_exit_one_and_name_the_path()
    {
        using var temp = new TempDirectory();
        string missing = temp.File("does-not-exist");
        var seams = BaseSeams(missing);

        Result result = Run(seams);

        Assert.Equal(DoctorCommand.ExitFailure, result.ExitCode);
        Assert.Contains(
            $"state path {missing} is missing",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(
            $"config path {Path.Combine(missing, "settings.json")} is missing",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("health: unhealthy", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_settings_file_exits_one_and_names_the_error()
    {
        using var temp = new TempDirectory();
        string state = temp.File("state");
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "settings.json"), "{ not json");
        var seams = BaseSeams(state);

        Result result = Run(seams);

        Assert.Equal(DoctorCommand.ExitFailure, result.ExitCode);
        Assert.Contains("Invalid settings in", result.Output, StringComparison.Ordinal);
        Assert.Contains("health: unhealthy", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_json_report_carries_every_documented_field_and_the_no_cache_answer()
    {
        using var fixture = new Fixture(updateCache: false);

        Result result = fixture.Run("--json");

        Assert.Equal(DoctorCommand.ExitOk, result.ExitCode);
        using JsonDocument document = JsonDocument.Parse(result.Output);
        JsonElement root = document.RootElement;
        foreach (
            string property in new[]
            {
                "version",
                "os",
                "architecture",
                "installChannel",
                "config",
                "state",
                "provider",
                "model",
                "credential",
                "containerEngines",
                "terminal",
                "updateCheck",
                "healthy",
                "problems",
            }
        )
        {
            Assert.True(root.TryGetProperty(property, out _), $"missing '{property}'");
        }

        JsonElement update = root.GetProperty("updateCheck");
        Assert.Equal(JsonValueKind.Null, update.GetProperty("checkedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, update.GetProperty("latestVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, update.GetProperty("changelogUrl").ValueKind);
        Assert.False(update.GetProperty("fresh").GetBoolean());
        Assert.True(root.GetProperty("healthy").GetBoolean());
        Assert.Empty(root.GetProperty("problems").EnumerateArray());
        Assert.False(
            File.Exists(Path.Combine(fixture.Root, "update-check.json")),
            "doctor must not consume the daily update-check request"
        );
    }

    [Theory]
    [InlineData("/home/u/.dotnet/tools/lunate", "dotnet tool")]
    [InlineData("/home/u/.local/bin/lunate", "self-contained")]
    [InlineData("/opt/homebrew/Cellar/lunate/0.5.0/bin/lunate", "Homebrew")]
    [InlineData("C:\\Users\\u\\scoop\\apps\\lunate\\current\\lunate.exe", "Scoop")]
    public void The_install_channel_is_read_from_the_executable_path(string path, string expected)
    {
        Assert.Equal(expected, InstallChannelDetector.Detect(path, baseDirectory: null));
    }

    [Fact]
    public void An_unknown_argument_is_a_usage_error_with_exit_two()
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();

        int exitCode = DoctorCommand.Run(["--verbose"], output, errors);

        Assert.Equal(DoctorCommand.ExitUsage, exitCode);
        Assert.Equal(DoctorCommand.Usage + Environment.NewLine, errors.ToString());
        Assert.Empty(output.ToString());
    }

    [Fact]
    public void Cli_help_and_dispatch_name_the_doctor_command()
    {
        using var help = new StringWriter();
        Assert.Equal(
            0,
            Cli.Run(["--help"], help, TextWriter.Null, TestContext.Current.CancellationToken)
        );
        Assert.Contains("lunate doctor [--json]", help.ToString(), StringComparison.Ordinal);

        using var output = new StringWriter();
        using var errors = new StringWriter();
        Assert.Equal(
            DoctorCommand.ExitUsage,
            Cli.Run(["doctor", "--verbose"], output, errors, TestContext.Current.CancellationToken)
        );
        Assert.Equal(DoctorCommand.Usage + Environment.NewLine, errors.ToString());
    }

    private static Result Run(DoctorSeams seams)
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        int exit = DoctorCommand.Run([], output, errors, seams);
        return new Result(exit, output.ToString(), errors.ToString());
    }

    private static DoctorSeams BaseSeams(string state) =>
        new()
        {
            StatePath = state,
            ModelsPath = Path.Combine(state, "models.json"),
            UpdateCheckCachePath = Path.Combine(state, "update-check.json"),
            Environment = name =>
                name switch
                {
                    "TERM" => "xterm-256color",
                    "LANG" => "en_US.UTF-8",
                    "PATH" => "/usr/bin",
                    _ => null,
                },
            ProcessPath = "/home/u/.local/bin/lunate",
            BaseDirectory = "/home/u/.local/bin",
            IsWindows = false,
            OsDescription = "macOS 15.0",
            Architecture = Architecture.Arm64,
            IsInteractive = false,
            ExecutableAvailable = name => name == "docker",
            Clock = () => Now,
            Version = "0.6.0-test",
        };

    private sealed record Result(int ExitCode, string Output, string Errors);

    private sealed class Fixture : IDisposable
    {
        private readonly DoctorSeams _seams;

        internal Fixture(bool updateCache = true)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "lunate-doctor-tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(Root);
            File.WriteAllText(SettingsPath, Settings);
            File.WriteAllText(
                Path.Combine(Root, "auth.json"),
                "{\"schemaVersion\":1,\"keys\":{\"openai\":\"" + Secret + "\"}}"
            );
            string cachePath = Path.Combine(Root, "update-check.json");
            if (updateCache)
            {
                UpdateCheckCache.Write(
                    cachePath,
                    new UpdateCheckCache(
                        Now.AddHours(-1),
                        "0.6.1",
                        "https://example.test/CHANGELOG.md"
                    )
                );
            }

            _seams = BaseSeams(Root);
        }

        internal string Root { get; }

        internal string SettingsPath => Path.Combine(Root, "settings.json");

        internal Result Run(params string[] args)
        {
            using var output = new StringWriter();
            using var errors = new StringWriter();
            int exit = DoctorCommand.Run(args, output, errors, _seams);
            return new Result(exit, output.ToString(), errors.ToString());
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
