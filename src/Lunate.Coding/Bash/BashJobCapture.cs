using System.Text;

namespace Lunate.Coding;

/// <summary>
/// One running job's unread output: chunks with a byte bound. Appending past the bound drops the
/// oldest chunks and records that it happened, so a command that writes faster than the model polls
/// can never grow the process's memory without limit. Draining returns only what has not been read.
/// </summary>
internal sealed class BashJobCapture(int cap)
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Queue<string> _chunks = [];
    private readonly Lock _gate = new();
    private int _bytes;
    private bool _dropped;

    public void Append(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        string chunk = text.ToString();
        int bytes = Utf8.GetByteCount(chunk);
        lock (_gate)
        {
            _chunks.Enqueue(chunk);
            _bytes += bytes;
            while (_bytes > cap && _chunks.Count > 0)
            {
                string oldest = _chunks.Dequeue();
                _bytes -= Utf8.GetByteCount(oldest);
                _dropped = true;
            }
        }
    }

    public (string Text, bool Dropped) Drain()
    {
        lock (_gate)
        {
            string text = string.Concat(_chunks);
            bool dropped = _dropped;
            _chunks.Clear();
            _bytes = 0;
            _dropped = false;
            return (text, dropped);
        }
    }
}
