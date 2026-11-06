using System.Net;
using System.Text;
using Lunate.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The model check through the real factory (B-4): the OpenCode Go preset declares
/// <c>x-opencode-session: {session.id}</c>, so a check built without a request identity throws
/// before the first byte. These tests run the real headers pipeline and assert the wire.
/// </summary>
public sealed class WizardModelCheckWireTests
{
    [Fact]
    public async Task The_model_check_sends_a_header_templated_preset_with_an_ephemeral_id()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire();
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));

        WizardCheckResult result = await WizardModelCheck.RunAsync(
            OpenCodeGoWizard(),
            catalog,
            wire.Factory(),
            () => "sk-test",
            context: null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        HttpRequestMessage request = Assert.Single(wire.Requests);
        string session = Assert.Single(request.Headers.GetValues("x-opencode-session"));
        Assert.False(string.IsNullOrWhiteSpace(session));
        Assert.Equal(
            "https://opencode.ai/inference/go/openai/v1/chat/completions",
            request.RequestUri!.AbsoluteUri
        );
    }

    [Fact]
    public async Task The_model_check_names_the_sessions_id_when_one_exists()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire();
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));

        WizardCheckResult result = await WizardModelCheck.RunAsync(
            OpenCodeGoWizard(),
            catalog,
            wire.Factory(),
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardProgress.Passed, result.Outcome);
        HttpRequestMessage request = Assert.Single(wire.Requests);
        Assert.Equal(
            "s_model-check",
            Assert.Single(request.Headers.GetValues("x-opencode-session"))
        );
    }

    private static SetupWizardModel OpenCodeGoWizard()
    {
        var wizard = new SetupWizardModel();
        wizard.RecordProvider(
            new WizardProviderChoice(
                "opencode-go",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        wizard.RecordCredential(new WizardCredentialChoice("opencode-go", Key: "sk-test"));
        wizard.RecordModel(new WizardModelChoice("opencode-go/kimi-k3", "openai-chat"));
        return wizard;
    }

    /// <summary>The wire the factory's transport finally reaches: every request, in order.</summary>
    private sealed class CapturedWire
    {
        private const string Response = """
            {
              "id": "chatcmpl-model-check",
              "object": "chat.completion",
              "created": 0,
              "model": "kimi-k3",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "OK" },
                  "finish_reason": "stop"
                }
              ],
              "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
            }
            """;

        private readonly List<HttpRequestMessage> _requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        public ChatClientFactory Factory()
        {
            var factory = new ChatClientFactory(
                NullLoggerFactory.Instance,
                enableOpenTelemetry: false
            );
            factory.BaseTransportFactory = () => new CaptureHandler(_requests, Response);
            return factory;
        }

        private sealed class CaptureHandler(List<HttpRequestMessage> requests, string response)
            : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                lock (requests)
                {
                    requests.Add(request);
                }

                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(response, Encoding.UTF8, "application/json"),
                    }
                );
            }
        }
    }
}
