using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using System.Text;

namespace Lunate.Tui;

internal sealed class KeyReader : IDisposable
{
    internal static readonly TimeSpan DefaultQuietWindow = TimeSpan.FromMilliseconds(50);

    private static readonly byte[] PasteStart = "\u001b[200~"u8.ToArray();
    private static readonly byte[] PasteEnd = "\u001b[201~"u8.ToArray();

    private readonly object _gate = new();
    private readonly IScheduler _scheduler;
    private readonly Action<KeyEvent> _onKey;
    private readonly Action<bool>? _onFocus;
    private readonly TimeSpan _quietWindow;
    private readonly List<byte> _buffer = [];
    private IDisposable? _escapeTimer;
    private bool _inPaste;
    private bool _disposed;

    public KeyReader(
        IScheduler scheduler,
        Action<KeyEvent> onKey,
        TimeSpan? quietWindow = null,
        Action<bool>? onFocus = null
    )
    {
        _scheduler = scheduler;
        _onKey = onKey;
        _quietWindow = quietWindow ?? DefaultQuietWindow;
        _onFocus = onFocus;
    }

    public void Feed(ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _escapeTimer?.Dispose();
            _escapeTimer = null;
            foreach (byte b in bytes)
            {
                _buffer.Add(b);
            }

            Process();
            ArmEscapeTimer();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _escapeTimer?.Dispose();
            _escapeTimer = null;
        }
    }

    /// <summary>
    /// Drops a partial sequence the decoder is still holding (a lone <c>Esc</c>, half an escape
    /// sequence, an unfinished paste). The terminal is changing hands: a byte of the next reader's
    /// would otherwise complete our sequence and both sides would see a wrong key.
    /// </summary>
    public void FlushPartial()
    {
        lock (_gate)
        {
            _escapeTimer?.Dispose();
            _escapeTimer = null;
            _buffer.Clear();
            _inPaste = false;
        }
    }

    private void Process()
    {
        while (_buffer.Count > 0)
        {
            if (_inPaste)
            {
                int end = IndexOf(_buffer, PasteEnd);
                if (end < 0)
                {
                    return;
                }

                var text = NormalizeNewlines(
                    Encoding.UTF8.GetString(CollectionsMarshal.AsSpan(_buffer)[..end])
                );
                _buffer.RemoveRange(0, end + PasteEnd.Length);
                _inPaste = false;
                var paste = new KeyEvent(KeyKind.Paste, text, false, false, false);
                InputTrace.Key(paste);
                _onKey(paste);
                continue;
            }

            if (StartsWith(_buffer, PasteStart))
            {
                _buffer.RemoveRange(0, PasteStart.Length);
                _inPaste = true;
                continue;
            }

            if (TryConsumeFocus(out bool focused))
            {
                _onFocus?.Invoke(focused);
                continue;
            }

            if (_buffer.Count == 1 && _buffer[0] == 0x1B)
            {
                return;
            }

            if (
                !VtInputDecoder.TryDecode(
                    CollectionsMarshal.AsSpan(_buffer),
                    out var key,
                    out int consumed
                )
                || consumed == 0
            )
            {
                return;
            }

            _buffer.RemoveRange(0, consumed);
            InputTrace.Key(key!);
            _onKey(key!);
        }
    }

    /// <summary>
    /// Consumes the xterm focus reports (<c>CSI I</c> and <c>CSI O</c>) before the key decoder can
    /// turn them into unknown keys. A paste keeps its literal text: this is checked only outside a
    /// bracketed paste.
    /// </summary>
    private bool TryConsumeFocus(out bool focused)
    {
        focused = false;
        if (
            _buffer.Count < 3
            || _buffer[0] != 0x1B
            || _buffer[1] != (byte)'['
            || _buffer[2] is not ((byte)'I' or (byte)'O')
        )
        {
            return false;
        }

        focused = _buffer[2] == (byte)'I';
        _buffer.RemoveRange(0, 3);
        return true;
    }

    private void ArmEscapeTimer()
    {
        if (_inPaste || _buffer.Count != 1 || _buffer[0] != 0x1B)
        {
            return;
        }

        _escapeTimer = _scheduler.Schedule(_quietWindow, EmitBufferedEscape);
    }

    private void EmitBufferedEscape()
    {
        lock (_gate)
        {
            _escapeTimer = null;
            if (_disposed || _inPaste || _buffer.Count != 1 || _buffer[0] != 0x1B)
            {
                return;
            }

            _buffer.Clear();
            var escape = new KeyEvent(KeyKind.Escape, null, false, false, false);
            InputTrace.Key(escape);
            _onKey(escape);
        }
    }

    private static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool StartsWith(List<byte> buffer, ReadOnlySpan<byte> prefix) =>
        buffer.Count >= prefix.Length
        && CollectionsMarshal.AsSpan(buffer)[..prefix.Length].SequenceEqual(prefix);

    private static int IndexOf(List<byte> buffer, ReadOnlySpan<byte> marker) =>
        CollectionsMarshal.AsSpan(buffer).IndexOf(marker);
}
