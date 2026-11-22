using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Lunate.Coding.Tests;

/// <summary>
/// The offline transport every web-tool test uses: it records the requests it saw and answers from
/// a caller-supplied function, so a response, a redirect chain, a transport failure and a timeout
/// are all deterministic and no test reaches the network.
/// </summary>
internal sealed class StubHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
) : HttpMessageHandler
{
    private readonly List<StubRequest> _requests = [];

    internal IReadOnlyList<StubRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    internal static StubHttpHandler Html(
        string body,
        HttpStatusCode status = HttpStatusCode.OK,
        string contentType = "text/html; charset=utf-8"
    ) => new((_, _) => Task.FromResult(Response(status, body, contentType)));

    internal static StubHttpHandler Text(
        string body,
        HttpStatusCode status = HttpStatusCode.OK,
        string contentType = "text/plain; charset=utf-8"
    ) => new((_, _) => Task.FromResult(Response(status, body, contentType)));

    internal static StubHttpHandler Redirect(
        string location,
        HttpStatusCode status = HttpStatusCode.Found
    ) =>
        new(
            (_, _) =>
            {
                var response = new HttpResponseMessage(status);
                response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
                return Task.FromResult(response);
            }
        );

    internal static StubHttpHandler Throws(Exception exception) =>
        new((_, _) => Task.FromException<HttpResponseMessage>(exception));

    internal static StubHttpHandler NeverCompletes() =>
        new(
            async (_, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );

    internal static HttpResponseMessage Response(
        HttpStatusCode status,
        string body,
        string contentType
    )
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new(status) { Content = content };
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        lock (_requests)
        {
            _requests.Add(
                new StubRequest(
                    request.Method.Method,
                    request.RequestUri,
                    request.Headers.UserAgent.ToString(),
                    string.Join(",", request.Headers.Accept.Select(value => value.MediaType))
                )
            );
        }

        return await respond(request, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record StubRequest(string Method, Uri? Uri, string UserAgent, string Accept);
