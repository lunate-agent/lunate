using System.Text;
using System.Text.Json;

namespace Lunate.Coding;

/// <summary>
/// The Tavily search API: a <c>POST</c> whose JSON body carries the query and the requested count,
/// the credential as a bearer token, and the JSON <c>results</c> array read as ranked results.
/// </summary>
internal sealed class TavilyBackend : WebSearchBackend
{
    internal static readonly Uri Endpoint = new("https://api.tavily.com/search", UriKind.Absolute);

    internal override string DisplayName => "Tavily";

    internal override bool NeedsCredential => true;

    internal override string Describe =>
        "Uses the Tavily search API with the configured credential.";

    internal override HttpRequestMessage CreateRequest(
        string query,
        int maxResults,
        string? credential
    )
    {
        string body = JsonSerializer.Serialize(new { query, max_results = maxResults });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + credential);
        return request;
    }

    internal override SearchPage Read(string body, Uri endpoint) =>
        SearchJson.Read(body, container: null, snippetProperty: "content")
        ?? new SearchPage([], NoResults: false);
}
