using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Lunate.Coding;

/// <summary>The readable part of one HTML document.</summary>
internal sealed record HtmlPage(string? Title, string Text);

/// <summary>
/// A bounded, quote-aware HTML reducer for <c>web_fetch</c>. It is deliberately small: no parser
/// package is added, malformed markup is read as far as it goes, script and style bodies never
/// become text, and an http(s) anchor becomes a markdown link resolved against the page URL.
/// </summary>
internal static partial class HtmlToText
{
    private static readonly HashSet<string> SkippedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "head",
        "script",
        "style",
        "noscript",
        "svg",
        "template",
    };

    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address",
        "article",
        "aside",
        "blockquote",
        "dd",
        "div",
        "dl",
        "dt",
        "fieldset",
        "figcaption",
        "figure",
        "footer",
        "form",
        "h1",
        "h2",
        "h3",
        "h4",
        "h5",
        "h6",
        "header",
        "hr",
        "main",
        "nav",
        "ol",
        "p",
        "pre",
        "section",
        "table",
        "tbody",
        "tfoot",
        "thead",
        "tr",
        "ul",
    };

    private static readonly Regex TagPattern = new(
        "<[^>]*>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    /// <summary>Reduces <paramref name="html"/> to its title and text for <paramref name="pageUri"/>.</summary>
    internal static HtmlPage Parse(string html, Uri pageUri)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(pageUri);

        string? title = ReadTitle(html);
        var output = new StringBuilder(html.Length / 2);
        var linkText = new StringBuilder();
        string? linkTarget = null;
        bool inLink = false;
        int index = 0;

        while (index < html.Length)
        {
            int open = html.IndexOf('<', index);
            if (open < 0)
            {
                AppendText(html[index..], inLink ? linkText : output);
                break;
            }

            if (open > index)
            {
                AppendText(html[index..open], inLink ? linkText : output);
            }

            if (StartsWith(html, open, "<!--"))
            {
                int end = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                index = end < 0 ? html.Length : end + 3;
                continue;
            }

            if (StartsWith(html, open, "<!") || StartsWith(html, open, "<?"))
            {
                int declarationEnd = FindTagEnd(html, open + 1);
                index = declarationEnd < 0 ? html.Length : declarationEnd + 1;
                continue;
            }

            int tagEnd = FindTagEnd(html, open + 1);
            if (tagEnd < 0)
            {
                break;
            }

            string tag = html[(open + 1)..tagEnd];
            index = tagEnd + 1;
            bool closing = tag.StartsWith('/');
            if (closing)
            {
                tag = tag[1..];
            }

            string name = TagName(tag);
            if (name.Length == 0)
            {
                continue;
            }

            if (!closing && SkippedElements.Contains(name))
            {
                index = SkipElement(html, index, name);
                continue;
            }

            if (!closing && name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                string? href = Attribute(tag, "href");
                linkTarget = href is null ? null : ResolveLink(href, pageUri);
                inLink = true;
                linkText.Clear();
                continue;
            }

            if (closing && name.Equals("a", StringComparison.OrdinalIgnoreCase))
            {
                FlushLink(output, linkText, linkTarget);
                inLink = false;
                linkTarget = null;
                continue;
            }

            if (name.Equals("li", StringComparison.OrdinalIgnoreCase))
            {
                if (closing)
                {
                    AppendBreak(output);
                }
                else
                {
                    AppendListItem(output);
                }
                continue;
            }

            if (name.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                AppendBreak(output);
                continue;
            }

            if (BlockElements.Contains(name))
            {
                AppendBreak(output);
            }
        }

        if (inLink)
        {
            FlushLink(output, linkText, linkTarget);
        }

        return new HtmlPage(title, Normalize(output.ToString()));
    }

    /// <summary>Strips tags and decodes entities, for a one-line title or search snippet.</summary>
    internal static string InlineText(string html) =>
        NormalizeInline(WebUtility.HtmlDecode(TagPattern.Replace(html, string.Empty)));
}
