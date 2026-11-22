using System.Globalization;
using System.Text;

namespace Lunate.Coding;

/// <summary>The shared bounds of the web tools: UTF-8-safe cuts and one capped body read.</summary>
internal static class WebText
{
    /// <summary>
    /// Cuts <paramref name="text"/> to at most <paramref name="maxBytes"/> UTF-8 bytes and appends
    /// a culture-invariant marker. The cut never splits a UTF-8 sequence or a surrogate pair.
    /// </summary>
    internal static string Cap(string text, int maxBytes, string subject)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        string marker = string.Create(
            CultureInfo.InvariantCulture,
            $"\n... [{subject} capped at {maxBytes} bytes] ..."
        );
        int budget = Math.Max(0, maxBytes - Encoding.UTF8.GetByteCount(marker));
        return CutUtf8(text, budget) + marker;
    }

    /// <summary>Cuts at the largest UTF-8 and surrogate boundary that fits the byte budget.</summary>
    internal static string CutUtf8(string text, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        int bytes = 0;
        int index = 0;
        while (index < text.Length)
        {
            int length =
                char.IsHighSurrogate(text[index])
                && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1])
                    ? 2
                    : 1;
            int size = Encoding.UTF8.GetByteCount(text.AsSpan(index, length));
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            index += length;
        }

        return text[..index];
    }

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/> from a response stream. Each read is bounded by
    /// the budget that is left, so the stream is never consumed beyond the cap.
    /// </summary>
    internal static async Task<byte[]> ReadCappedAsync(
        Stream stream,
        int maxBytes,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxBytes <= 0)
        {
            return [];
        }

        using var output = new MemoryStream();
        byte[] buffer = new byte[Math.Min(16 * 1024, maxBytes)];
        while ((int)output.Length < maxBytes)
        {
            int remaining = maxBytes - (int)output.Length;
            int read = await stream
                .ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    /// <summary>Decodes a response body as UTF-8, dropping a byte-order mark when present.</summary>
    internal static string Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ReadOnlySpan<byte> span = bytes;
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            span = span[3..];
        }

        return Encoding.UTF8.GetString(span);
    }
}
