using System.Text;

namespace Lunate.Coding.Tests;

/// <summary>
/// A read-only body that counts the bytes it hands out, so a test can observe how much of a
/// response was consumed rather than only what the caller kept.
/// </summary>
internal sealed class CountingReadStream(byte[] body) : Stream
{
    private int _position;

    internal int BytesRead { get; private set; }

    internal static CountingReadStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => body.Length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        int available = Math.Min(buffer.Length, body.Length - _position);
        if (available <= 0)
        {
            return 0;
        }

        body.AsSpan(_position, available).CopyTo(buffer);
        _position += available;
        BytesRead += available;
        return available;
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
