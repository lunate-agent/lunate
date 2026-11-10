using System.Runtime.InteropServices;

namespace Lunate.Tui.Platform;

internal static class UnixRawMode
{
    internal const int StandardInputFd = 0;
    internal const int ActionNow = 0;
    internal const int TermiosBufferSize = 128;

    // The buffer covers both layouts (Darwin 72 bytes, Linux 60) with slack; the
    // per-platform offsets live in TermiosLayout and are pinned by tests.
    internal static TermiosLayout Current { get; } =
        OperatingSystem.IsMacOS() ? TermiosLayout.MacOs : TermiosLayout.Linux;

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, byte[] termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int optionalActions, byte[] termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcflush(int fd, int queueSelector);

    public static ITerminalMode Enter(int fd = StandardInputFd)
    {
        var state = new byte[TermiosBufferSize];
        if (tcgetattr(fd, state) != 0)
        {
            throw new InvalidOperationException(
                "the terminal attributes of stdin could not be read; interactive input is unavailable."
            );
        }

        var original = (byte[])state.Clone();
        Current.ApplyRaw(state);

        if (tcsetattr(fd, ActionNow, state) != 0)
        {
            throw new InvalidOperationException(
                "the terminal attributes of stdin could not be changed; interactive input is unavailable."
            );
        }

        // Suspend and resume swap the original and the raw attribute blocks; the restore — the
        // path /quit's scope exit and a signal handler both reach — writes the original back.
        return new SerializedTerminalMode(
            suspend: () => tcsetattr(fd, ActionNow, original) == 0,
            resume: () => tcsetattr(fd, ActionNow, state) == 0,
            restore: () => Restore(fd, original)
        );
    }

    private static bool Restore(int fd, byte[] original)
    {
        if (tcsetattr(fd, ActionNow, original) != 0)
        {
            return false;
        }

        // XNU's tcsetattr sets PENDIN unconditionally when the write turns ICANON back on
        // (bsd/kern/tty.c), and only a read or an input flush clears it. A key pump still running
        // during exit may or may not read first, so without the flush the restored attributes
        // sometimes keep PENDIN. Flushing the input queue makes the restored attributes the
        // captured ones deterministically and drops the type-ahead a dying session cannot show.
        _ = tcflush(fd, Current.InputFlush);
        return true;
    }
}
