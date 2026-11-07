using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Lunate.Ai;
using Microsoft.Extensions.AI;
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
            AuthStore.Load(temp.File("auth.json"), _ => null),
            _ => null,
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
        string body = wire.Bodies[0];
        Assert.Contains("\"model\":\"kimi-k3\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("opencode-go/kimi-k3", body, StringComparison.Ordinal);
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
            AuthStore.Load(temp.File("auth.json"), _ => null),
            _ => null,
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


    [Fact]
    public async Task The_model_check_and_the_first_session_request_match_except_for_the_prompt()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire();
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));
        AuthStore auth = AuthStore.Load(temp.File("auth.json"), _ => null);
        SetupWizardModel wizard = OpenCodeGoWizard();

        await WizardModelCheck.RunAsync(
            wizard,
            catalog,
            wire.Factory(),
            auth,
            _ => null,
            () => "sk-test",
            new RequestContext("s_model-check"),
            TestContext.Current.CancellationToken
        );

        ResolvedModelProfile profile = ModelProfileResolver.Resolve(
            new ModelProfile("work", "opencode-go", "opencode-go/kimi-k3", ApiFamily: "openai-chat"),
            [SetupWizardCommit.StagedEntry(wizard)],
            catalog
        );
        IChatClient session = DecisionUsageModels.CreateUsageClient(
            profile,
            auth,
            _ => null,
            wire.Factory(),
            new RequestContext("s_model-check"),
            credentialOverride: "sk-test"
        );
        await session.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "the first session prompt")],
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Equal(2, wire.Requests.Count);
        string check = wire.Bodies[0];
        string first = wire.Bodies[1];
        Assert.Equal(WithoutPrompt(check), WithoutPrompt(first));
        Assert.Equal(
            wire.Requests[0].RequestUri!.AbsoluteUri,
            wire.Requests[1].RequestUri!.AbsoluteUri
        );
    }

    private static string WithoutPrompt(string json)
    {
        JsonNode root = JsonNode.Parse(json)!;
        root["messages"] = null;
        return root.ToJsonString();
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
        private readonly List<string> _bodies = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        public IReadOnlyList<string> Bodies => _bodies;

        public ChatClientFactory Factory()
        {
            var factory = new ChatClientFactory(
                NullLoggerFactory.Instance,
                enableOpenTelemetry: false
            );
            factory.BaseTransportFactory = () => new CaptureHandler(_requests, _bodies, Response);
            return factory;
        }

        private sealed class CaptureHandler(
            List<HttpRequestMessage> requests,
            List<string> bodies,
            string response
        ) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                string body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                lock (requests)
                {
                    requests.Add(request);
                    bodies.Add(body);
                }

                return (
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(response, Encoding.UTF8, "application/json"),
                    }
                );
            }
        }
    }
}
