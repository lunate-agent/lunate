using System.Net;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The wizard's connection test against offline fixtures (spec: The full walk, step 3; design §3):
/// one real discovery request through the shipped client with the injected transport, every
/// failure shape named — network, authentication (401, 403), an undecipherable response — and the
/// success listing carrying the ids the model step picks from. The credential never lands in an
/// error or a URL.
/// </summary>
public sealed class WizardDiscoveryTests
{
    private static readonly WizardProviderChoice Preset = new(
        "openai",
        Custom: false,
        Region: null,
        Endpoint: null,
        ApiFamily: "openai-chat",
        Placement: CredentialPlacement.Bearer,
        ModelsUrl: null
    );

    private static readonly WizardProviderChoice Custom = new(
        "homelab",
        Custom: true,
        Region: null,
        Endpoint: new Uri("http://localhost:8080/v1"),
        ApiFamily: "openai-chat",
        Placement: CredentialPlacement.Bearer,
        ModelsUrl: null
    );

    private static readonly Func<string, string?> EmptyEnvironment = _ => null;

    private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(response);
    }

    [Fact]
    public async Task A_preset_discovery_lists_the_response_models()
    {
        RecordingHandler transport = new(
            (_, _) => Task.FromResult(Response(200, "{\"data\":[{\"id\":\"gpt-5\"}]}"))
        );

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            _ => "preset-env-key",
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Passed);
        KeyValuePair<string, string?>? header = transport.Requests.Single().Headers.Authorization
            is { } authorization
            ? new KeyValuePair<string, string?>("Authorization", authorization.ToString())
            : null;
        Assert.Equal("Bearer preset-env-key", header!.Value.Value);
        Assert.Equal("gpt-5", Assert.Single(result.Models).Id);
    }

    [Fact]
    public async Task The_typed_custom_key_reaches_the_custom_endpoint()
    {
        RecordingHandler transport = new(
            (_, _) => Task.FromResult(Response(200, "{\"data\":[{\"id\":\"local-7b\"}]}"))
        );

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Custom,
            new WizardCredentialChoice("homelab", Key: "typed-key"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(result.Passed);
        HttpRequestMessage request = transport.Requests.Single();
        Assert.Equal("http://localhost:8080/v1/models", request.RequestUri!.ToString());
        Assert.Equal("Bearer typed-key", request.Headers.Authorization!.ToString());
        Assert.Equal("local-7b", Assert.Single(result.Models).Id);
    }

    [Fact]
    public async Task The_custom_placement_rides_the_request()
    {
        RecordingHandler transport = new(
            (_, _) => Task.FromResult(Response(200, "{\"data\":[{\"id\":\"m\"}]}"))
        );

        await WizardDiscovery.RunAsync(
            Custom with
            {
                Placement = new CredentialPlacement("x-api-key"),
            },
            new WizardCredentialChoice("homelab", Key: "typed-key"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.True(
            transport.Requests.Single().Headers.Contains("x-api-key"),
            "the declared placement carries the credential"
        );
    }

    [Fact]
    public async Task An_unauthorized_key_names_authentication()
    {
        RecordingHandler transport = new((_, _) => Task.FromResult(Response(401, "{}")));

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.False(result.Passed);
        Assert.Equal(WizardFailureKind.Authentication, result.FailureKind);
        Assert.Contains("401", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_forbidden_key_names_authentication_too()
    {
        RecordingHandler transport = new((_, _) => Task.FromResult(Response(403, "{}")));

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardFailureKind.Authentication, result.FailureKind);
        Assert.Contains("403", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_connection_names_the_network()
    {
        RecordingHandler transport = new(
            (_, _) => throw new HttpRequestException("connection refused")
        );

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardFailureKind.Network, result.FailureKind);
        Assert.Contains("connection refused", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_undecipherable_response_is_named()
    {
        RecordingHandler transport = new(
            (_, _) => Task.FromResult(Response(200, "not json at all"))
        );

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardFailureKind.Undecipherable, result.FailureKind);
    }

    [Fact]
    public async Task Another_status_is_rejected_with_it_named()
    {
        RecordingHandler transport = new((_, _) => Task.FromResult(Response(500, "{}")));

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        Assert.Equal(WizardFailureKind.Rejected, result.FailureKind);
        Assert.Contains("500", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_discovered_metadata_rides_the_listing()
    {
        RecordingHandler transport = new(
            (_, _) =>
                Task.FromResult(
                    Response(
                        200,
                        """
                        {"data":[{"id":"m1","limit":{"context":64000},"capabilities":{"reasoning":{"supported":true,"effort":["low","high"]}},"api":{"chat-completions":{}}}]}
                        """
                    )
                )
        );

        WizardDiscoveryResult result = await WizardDiscovery.RunAsync(
            Preset,
            new WizardCredentialChoice("env:OPENAI_API_KEY"),
            EmptyEnvironment,
            handler: transport,
            ct: TestContext.Current.CancellationToken
        );

        WizardModelListing listing = Assert.Single(result.Models);
        Assert.Equal("m1", listing.Id);
        Assert.Equal(64000, listing.ContextWindow);
        Assert.Equal("effort", listing.Reasoning);
        Assert.Equal("openai-chat", listing.ApiFamily);
    }

    private static HttpResponseMessage Response(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };
}
