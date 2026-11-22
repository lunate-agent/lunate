using System.Text;

namespace Lunate.Coding;

/// <summary>The job's two pipe pumps: one decoder per stream, appending bounded chunks.</summary>
internal sealed partial class BashJob
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static async Task PumpAsync(Stream stream, BashJobCapture capture)
    {
        Decoder decoder = Utf8.GetDecoder();
        var buffer = new byte[8192];
        var chars = new char[8192];
        while (true)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false);
            }
            catch (Exception exception) when (LocalProcessStreams.IsPipeClosed(exception))
            {
                break;
            }

            if (read == 0)
            {
                break;
            }

            int count = decoder.GetChars(buffer, 0, read, chars, 0, flush: false);
            if (count > 0)
            {
                capture.Append(chars.AsSpan(0, count));
            }
        }

        int tail = decoder.GetChars([], 0, 0, chars, 0, flush: true);
        if (tail > 0)
        {
            capture.Append(chars.AsSpan(0, tail));
        }
    }
}
