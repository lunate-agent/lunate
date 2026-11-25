using System.Net;
using System.Text.RegularExpressions;

namespace Lunate.Coding;

/// <summary>
/// The HTML reading both HTML backends share: anchors, their classed attributes, absolute http(s)
/// URL resolution, and the genuine no-results marker. A page with no result links counts as an
/// empty search only when one of its elements carries a no-results class and the marker text inside
/// it; marker-looking text echoed elsewhere on a challenge page does not count.
/// </summary>
internal static partial class SearchHtml
{
    private static readonly string[] NoResultsTextMarkers = ["No results found", "No results."];

    internal static readonly Regex AnchorPattern = new(
        """<a\b(?<attrs>[^>]*)>(?<text>.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    private static readonly Regex OpenTagPattern = new(
        """<(?<tag>[a-z][a-z0-9]*)\b(?<attrs>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    /// <summary>An element's class attribute, or null when it has none.</summary>
    internal static string ClassAttribute(string attributes) =>
        HtmlToText.Attribute(attributes, "class") ?? string.Empty;

    /// <summary>
    /// One absolute http(s) URL from a link target: HTML entities decoded, a scheme-relative target
    /// resolved against <paramref name="baseUri"/>, and anything else resolved against it or
    /// refused. A relative target with no base is refused.
    /// </summary>
    internal static string? AbsoluteUrl(string? href, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        string value = WebUtility.HtmlDecode(href).Trim();
        if (baseUri is not null && value.StartsWith("//", StringComparison.Ordinal))
        {
            value = baseUri.Scheme + ":" + value;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute))
        {
            return absolute.Scheme is "http" or "https" ? absolute.AbsoluteUri : null;
        }

        if (baseUri is not null && Uri.TryCreate(baseUri, value, out absolute))
        {
            return absolute.Scheme is "http" or "https" ? absolute.AbsoluteUri : null;
        }

        return null;
    }

    /// <summary>Whether the page carries a genuine no-results element and marker.</summary>
    internal static bool LooksLikeNoResults(string html)
    {
        foreach (Match element in OpenTagPattern.Matches(html))
        {
            if (!HasNoResultsClass(HtmlToText.Attribute(element.Groups["attrs"].Value, "class")))
            {
                continue;
            }

            int contentStart = element.Index + element.Length;
            int contentEnd = html.IndexOf(
                "</" + element.Groups["tag"].Value,
                contentStart,
                StringComparison.OrdinalIgnoreCase
            );
            if (contentEnd < 0)
            {
                continue;
            }

            string text = HtmlToText.InlineText(html[contentStart..contentEnd]);
            foreach (string marker in NoResultsTextMarkers)
            {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasNoResultsClass(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
        {
            return false;
        }

        foreach (
            string token in classes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        )
        {
            if (
                token.Equals("no-results", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("no-results__", StringComparison.OrdinalIgnoreCase)
                || token.StartsWith("no-results--", StringComparison.OrdinalIgnoreCase)
            )
            {
                return true;
            }
        }

        return false;
    }
}
