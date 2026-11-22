using Lunate.TestSupport;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// The startup path: constructing a client for one family must not load a sibling family's SDK.
/// The probe runs in a child process because the test runner may already have loaded Google.GenAI
/// through another test in this assembly; only a fresh process observes the startup state.
/// </summary>
[Collection(ChatClientFactoryEnvCollection.Name)]
public sealed class ChatClientFactoryStartupTests
{
    private const string Method = nameof(An_anthropic_client_does_not_load_google_genai);

    [Fact]
    public async Task An_anthropic_client_does_not_load_google_genai()
    {
        if (TestChildProcess.IsChild(Method))
        {
            using var environment = new EnvironmentScope(
                ("ANTHROPIC_API_KEY", "fixture-only-key"),
                ("LUNATE_OTEL", null),
                ("OTEL_EXPORTER_OTLP_ENDPOINT", null),
                ("LUNATE_RECORD", null),
                ("LUNATE_REPLAY", null)
            );
            Assert.Empty(GoogleGenAiAssemblies());

            var factory = new ChatClientFactory(new MarkingLoggerFactory(static () => { }));
            var model = new ModelInfo(
                "claude-probe",
                "anthropic",
                Endpoint: null,
                ContextWindow: 8_192,
                SupportsTools: true
            )
            {
                ApiFamily = ProviderApiFamilies.AnthropicMessages,
            };
            using IChatClient client = factory.Create(model);

            Assert.Empty(GoogleGenAiAssemblies());
            return;
        }

        (int exitCode, string output) = await TestChildProcess.RunAsync(
            typeof(ChatClientFactoryStartupTests),
            Method
        );

        Assert.True(
            exitCode == 0,
            $"the Google.GenAI startup probe failed (exit {exitCode}):\n{output}"
        );
    }

    private static string[] GoogleGenAiAssemblies() =>
        [
            .. AppDomain
                .CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name ?? string.Empty)
                .Where(name => name.StartsWith("Google.GenAI", StringComparison.Ordinal)),
        ];
}
