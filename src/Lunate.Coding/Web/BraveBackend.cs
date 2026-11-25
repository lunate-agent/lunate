using System.Globalization;
using System.Text;

namespace Lunate.Coding;

/// <summary>
/// The Brave Search API: a <c>GET</c> with the query and the requested count, the credential in
/// <c>X-Subscription-Token</c>, and the JSON <c>web.results</c> array read as ranked results.
/// </summary>
internal sealed class BraveBackend : WebSearchBackend
{
    internal static readonly Uri Endpoint = new(
        "https://api.search.brave.com/res/v1/web/search",
        UriKind.Absolute
    );

    internal override string DisplayName => "Brave Search";

    internal override bool NeedsCredential => true;

    internal override string Describe =>
        "Uses the Brave Search API with the configured credential.";

    internal override HttpRequestMessage CreateRequest(
        string query,
        int maxResults,
        string? credential
    )
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(
                Endpoint.AbsoluteUri
                    + "?q="
                    + Uri.EscapeDataString(query)
                    + "&count="
                    + maxResults.ToString(CultureInfo.InvariantCulture),
                UriKind.Absolute
            )
        );
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.TryAddWithoutValidation("X-Subscription-Token", credential);
        return request;
    }

    internal override SearchPage Read(string body, Uri endpoint) =>
        SearchJson.Read(body, container: "web", snippetProperty: "description")
        ?? new SearchPage([], NoResults: false);
}
