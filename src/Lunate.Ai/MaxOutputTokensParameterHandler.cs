using System.Net.Http.Headers;
using System.Text;

namespace Lunate.Ai;

/// <summary>
/// Renames the OpenAI SDK's <c>max_completion_tokens</c> body field to the parameter the wire's
/// backend accepts — <c>max_tokens</c> for the OpenAI-compatible presets that read only the older
/// name. The SDK hard-codes <c>max_completion_tokens</c> on the chat-completions body, so this
/// handler sits on the transport beside the credential handler and rewrites only that one key:
/// the streaming pipeline, the headers and the credential placement all stay exactly as the SDK
/// built them.
/// </summary>
internal sealed class MaxOutputTokensParameterHandler(string parameterName) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.Content is { } content)
        {
            string body = await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string rewritten = body.Replace(
                "\"max_completion_tokens\"",
                $"\"{parameterName}\"",
                StringComparison.Ordinal
            );
            if (!string.Equals(body, rewritten, StringComparison.Ordinal))
            {
                MediaTypeHeaderValue? contentType = content.Headers.ContentType;
                var replacement = new StringContent(rewritten, Encoding.UTF8);
                if (contentType is not null)
                {
                    replacement.Headers.ContentType = contentType;
                }

                request.Content = replacement;
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
