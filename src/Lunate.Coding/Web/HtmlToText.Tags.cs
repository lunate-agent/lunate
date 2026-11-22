namespace Lunate.Coding;

internal static partial class HtmlToText
{
    /// <summary>The value of <paramref name="name"/> from one tag's attribute text, quote-aware.</summary>
    internal static string? Attribute(string tag, string name)
    {
        int index = 0;
        while (index < tag.Length && !char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        while (index < tag.Length)
        {
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            int nameStart = index;
            while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] != '=')
            {
                index++;
            }

            string attribute = tag[nameStart..index];
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length || tag[index] != '=')
            {
                continue;
            }

            index++;
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            string value;
            if (index < tag.Length && tag[index] is '"' or '\'')
            {
                char quote = tag[index++];
                int start = index;
                while (index < tag.Length && tag[index] != quote)
                {
                    index++;
                }

                value = tag[start..index];
                if (index < tag.Length)
                {
                    index++;
                }
            }
            else
            {
                int start = index;
                while (index < tag.Length && !char.IsWhiteSpace(tag[index]))
                {
                    index++;
                }

                value = tag[start..index];
            }

            if (attribute.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static int SkipElement(string html, int start, string name)
    {
        int index = start;
        while (index < html.Length)
        {
            int open = html.IndexOf('<', index);
            if (open < 0)
            {
                return html.Length;
            }

            if (StartsWith(html, open, "<!--"))
            {
                int end = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                if (end < 0)
                {
                    return html.Length;
                }

                index = end + 3;
                continue;
            }

            int tagEnd = FindTagEnd(html, open + 1);
            if (tagEnd < 0)
            {
                return html.Length;
            }

            string tag = html[(open + 1)..tagEnd].TrimStart();
            if (
                tag.StartsWith('/')
                && TagName(tag[1..].TrimStart()).Equals(name, StringComparison.OrdinalIgnoreCase)
            )
            {
                return tagEnd + 1;
            }

            index = tagEnd + 1;
        }

        return html.Length;
    }

    private static int FindTagEnd(string html, int start)
    {
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        for (int index = start; index < html.Length; index++)
        {
            char character = html[index];
            if (character == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
            }
            else if (character == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
            }
            else if (character == '>' && !inSingleQuote && !inDoubleQuote)
            {
                return index;
            }
        }

        return -1;
    }

    private static string TagName(string tag)
    {
        int index = 0;
        while (
            index < tag.Length
            && !char.IsWhiteSpace(tag[index])
            && tag[index] != '/'
            && tag[index] != '>'
        )
        {
            index++;
        }

        return tag[..index];
    }

    private static bool StartsWith(string value, int index, string prefix) =>
        value.AsSpan(index).StartsWith(prefix, StringComparison.Ordinal);
}
