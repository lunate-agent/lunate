using System.Net;
using System.Text;

namespace Lunate.Coding;

internal static partial class HtmlToText
{
    private static string? ReadTitle(string html)
    {
        int start = html.IndexOf("<title", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        int tagEnd = FindTagEnd(html, start + 1);
        if (tagEnd < 0)
        {
            return null;
        }

        int close = html.IndexOf("</title", tagEnd + 1, StringComparison.OrdinalIgnoreCase);
        if (close < 0)
        {
            return null;
        }

        string title = InlineText(html[(tagEnd + 1)..close]);
        return title.Length == 0 ? null : title;
    }

    private static void AppendText(string raw, StringBuilder target)
    {
        string decoded = WebUtility.HtmlDecode(raw);
        foreach (char character in decoded)
        {
            char value = character == '\u00A0' ? ' ' : character;
            if (char.IsWhiteSpace(value))
            {
                if (target.Length > 0 && target[^1] != ' ' && target[^1] != '\n')
                {
                    target.Append(' ');
                }

                continue;
            }

            target.Append(value);
        }
    }

    private static void AppendBreak(StringBuilder output)
    {
        if (output.Length == 0)
        {
            return;
        }

        if (output[^1] != '\n')
        {
            output.Append('\n');
        }
        else if (output.Length < 2 || output[^2] != '\n')
        {
            output.Append('\n');
        }
    }

    private static void AppendListItem(StringBuilder output)
    {
        if (output.Length > 0 && output[^1] != '\n')
        {
            output.Append('\n');
        }

        output.Append("- ");
    }

    private static void FlushLink(StringBuilder output, StringBuilder linkText, string? linkTarget)
    {
        string text = NormalizeInline(linkText.ToString());
        if (text.Length == 0)
        {
            return;
        }

        if (linkTarget is null)
        {
            output.Append(text);
            return;
        }

        output.Append('[').Append(text).Append("](").Append(linkTarget).Append(')');
    }

    private static string? ResolveLink(string href, Uri pageUri)
    {
        string value = WebUtility.HtmlDecode(href).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = pageUri.Scheme + ":" + value;
        }

        if (Uri.TryCreate(pageUri, value, out Uri? target) && target.Scheme is "http" or "https")
        {
            return target.AbsoluteUri;
        }

        return null;
    }

    private static string Normalize(string value)
    {
        string[] lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var output = new StringBuilder(value.Length);
        bool pendingBlank = false;
        foreach (string raw in lines)
        {
            string line = raw.TrimEnd();
            if (line.Length == 0)
            {
                pendingBlank = output.Length > 0;
                continue;
            }

            if (output.Length > 0)
            {
                output.Append('\n');
                if (pendingBlank)
                {
                    output.Append('\n');
                }
            }

            output.Append(line);
            pendingBlank = false;
        }

        return output.ToString();
    }

    private static string NormalizeInline(string value)
    {
        var output = new StringBuilder(value.Length);
        bool pendingSpace = false;
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = output.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                output.Append(' ');
                pendingSpace = false;
            }

            output.Append(character);
        }

        return output.ToString();
    }
}
