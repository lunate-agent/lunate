using Lunate.Ai;

namespace Lunate.Coding;

internal enum ProviderReachability
{
    Reachable,
    Unreachable,
    Unknown,
}

/// <summary>
/// A bounded unauthenticated connectivity probe for a provider endpoint. It is a provider request
/// like any other, so it names this client (<c>User-Agent: lunate/&lt;version&gt;</c>, the value every
/// provider transport renders); it carries no credential and no provider-declared header, because a
/// reachability check must not place a key on an endpoint it has not verified.
/// </summary>
internal static class ProviderReachabilityProbe
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public static ProviderReachability Check(HttpClient client, Uri endpoint, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        TimeSpan boundedTimeout =
            timeout <= TimeSpan.Zero || timeout > DefaultTimeout ? DefaultTimeout : timeout;

        using var cancellation = new CancellationTokenSource(boundedTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
        // Exactly one agent, and it is ours: remove first so a caller's default cannot double it.
        request.Headers.Remove("User-Agent");
        request.Headers.TryAddWithoutValidation("User-Agent", RequestContext.LunateVersion);
        try
        {
            using HttpResponseMessage _ = client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token)
                .WaitAsync(boundedTimeout)
                .GetAwaiter()
                .GetResult();
            return ProviderReachability.Reachable;
        }
        catch (HttpRequestException)
        {
            return ProviderReachability.Unreachable;
        }
        catch (OperationCanceledException)
        {
            return ProviderReachability.Unknown;
        }
        catch (TimeoutException)
        {
            return ProviderReachability.Unknown;
        }
    }
}
