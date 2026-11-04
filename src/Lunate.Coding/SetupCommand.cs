using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>Injected paths and boundaries for <see cref="SetupCommand"/>; null is the production default.</summary>
internal sealed record SetupCommandSeams(
    string? AuthPath = null,
    string? SettingsPath = null,
    string? ModelsPath = null,
    Func<string, string?>? Environment = null,
    TextReader? Input = null,
    bool? StdinIsTerminal = null,
    HttpMessageHandler? DiscoveryTransport = null,
    IChatClientFactory? ChatFactory = null
);

/// <summary>
/// The <c>lunate setup</c> flag path (spec: Setup command line): every answer is a flag, absent
/// flags skip steps and invent nothing, and the commit rides the same store writers the
/// interactive wizard commits through. The connection test and the model check run unless
/// suppressed; the summary prints stdout as one human-readable block and never prints a
/// credential. Exit codes: 0 after a real commit or a no-op; 2 when a value needs interaction
/// or a flag — the missing answer is named; 2 for usage errors too.
/// </summary>
internal static partial class SetupCommand
{
    internal const int ExitOk = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUsage = 2;

    internal const string Usage =
        "Usage: lunate setup [--provider <preset-id>] [--endpoint <url>] "
        + "[--api-family <family>] [--credential-placement <bearer|x-api-key>] "
        + "[--models-url <url>] [--credential <env:VARIABLE|auth:name> | --key-stdin] "
        + "[--model <id>] [--profile <name>] [--thinking <low|medium|high>] [--fast] "
        + "[--as-decision] [--no-test] [--no-model-check] [--force]";

    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter errors,
        SetupCommandSeams? seams = null,
        CancellationToken ct = default
    )
    {
        try
        {
            if (!Parse(args, out FlagAnswers answers, out string parseError))
            {
                errors.WriteLine($"lunate: {parseError}");
                return ExitUsage;
            }

            return WalkAsync(answers, seams ?? new SetupCommandSeams(), output, errors, ct)
                .GetAwaiter()
                .GetResult();
        }
        catch (UsageException exception)
        {
            errors.WriteLine($"lunate: {exception.Message}; {Usage}");
            return ExitUsage;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Store and load errors name keys and paths, never values.
            errors.WriteLine($"lunate: {exception.Message}");
            return ExitFailure;
        }
    }

    private static bool Parse(string[] args, out FlagAnswers answers, out string error)
    {
        answers = new FlagAnswers();
        error = string.Empty;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--provider":
                    answers.Provider = Take(args, index++);
                    break;
                case "--endpoint":
                    answers.Endpoint = Take(args, index++);
                    break;
                case "--api-family":
                    answers.ApiFamily = Take(args, index++);
                    break;
                case "--credential-placement":
                    answers.CredentialPlacement = Take(args, index++);
                    break;
                case "--models-url":
                    answers.ModelsUrl = Take(args, index++);
                    break;
                case "--credential":
                    answers.Credential = Take(args, index++);
                    break;
                case "--key-stdin":
                    answers.KeyStdin = true;
                    break;
                case "--model":
                    answers.Model = Take(args, index++);
                    break;
                case "--profile":
                    answers.Profile = Take(args, index++);
                    break;
                case "--thinking":
                    answers.Thinking = Take(args, index++);
                    break;
                case "--fast":
                    answers.Fast = true;
                    break;
                case "--as-decision":
                    answers.AsDecision = true;
                    break;
                case "--no-test":
                    answers.NoTest = true;
                    break;
                case "--no-model-check":
                    answers.NoModelCheck = true;
                    break;
                case "--force":
                    answers.Force = true;
                    break;
                default:
                    error = $"unknown flag '{args[index]}'";
                    return false;
            }
        }

        return true;
    }

    private static string Take(string[] args, int index)
    {
        string flag = args[index];
        if (index + 1 >= args.Length)
        {
            throw new UsageException($"the flag '{flag}' needs a value");
        }

        return args[index + 1];
    }

    internal sealed record FlagAnswers
    {
        public string? Provider { get; set; }

        public string? Endpoint { get; set; }

        public string? ApiFamily { get; set; }

        public string? CredentialPlacement { get; set; }

        public string? ModelsUrl { get; set; }

        public string? Credential { get; set; }

        public bool KeyStdin { get; set; }

        public string? Model { get; set; }

        public string? Profile { get; set; }

        public string? Thinking { get; set; }

        public bool Fast { get; set; }

        public bool AsDecision { get; set; }

        public bool NoTest { get; set; }

        public bool NoModelCheck { get; set; }

        public bool Force { get; set; }
    }

    internal sealed class UsageException(string message) : InvalidOperationException(message);
}
