using System.Text;

namespace Lunate.Coding;

/// <summary>
/// Reads UTF-8 lines from one stream with an optional byte bound. A line over the bound is
/// drained to its newline and rejected with <see cref="InvalidDataException"/> — the reader
/// never grows past the bound, so a hostile child cannot OOM the host. A trailing <c>\r</c> is
/// stripped; the last line without a newline is returned at EOF.
/// </summary>
internal sealed class BoundedLineReader(Stream stream, int? maxLineBytes)
{
    private const int ChunkSize = 8192;

    private readonly byte[] _pending = new byte[ChunkSize];
    private int _start;
    private int _count;

    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (true)
        {
            int newline = IndexOfNewline(_pending, _start, _count);
            if (newline >= 0)
            {
                line.AddRange(new ArraySegment<byte>(_pending, _start, newline - _start));
                Advance(newline + 1);
                if (maxLineBytes is { } reached && line.Count > reached)
                {
                    throw new InvalidDataException(
                        $"a line over {reached.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes arrived on the child's stream; the line was drained and discarded."
                    );
                }

                return Decode(line);
            }

            if (_count > 0)
            {
                line.AddRange(new ArraySegment<byte>(_pending, _start, _count));
                _start = 0;
                _count = 0;
                if (maxLineBytes is { } bound && line.Count > bound)
                {
                    await DrainToNewlineAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidDataException(
                        $"a line over {bound.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes arrived on the child's stream; the line was drained and discarded."
                    );
                }
            }

            int read = await stream
                .ReadAsync(_pending.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return line.Count == 0 ? null : Decode(line);
            }

            _start = 0;
            _count = read;
        }
    }

    private void Advance(int next)
    {
        _count -= next - _start;
        _start = next;
    }

    private async ValueTask DrainToNewlineAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            int newline = IndexOfNewline(_pending, _start, _count);
            if (newline >= 0)
            {
                Advance(newline + 1);
                return;
            }

            int read = await stream
                .ReadAsync(_pending.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                _start = 0;
                _count = 0;
                return;
            }

            _start = 0;
            _count = read;
        }
    }

    private static int IndexOfNewline(byte[] buffer, int start, int count)
    {
        for (var index = start; index < start + count; index++)
        {
            if (buffer[index] == (byte)'\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static string Decode(List<byte> bytes)
    {
        if (bytes.Count > 0 && bytes[^1] == (byte)'\r')
        {
            bytes.RemoveAt(bytes.Count - 1);
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }
}
