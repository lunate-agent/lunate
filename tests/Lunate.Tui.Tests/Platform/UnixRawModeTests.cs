using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Lunate.Tui.Platform;

namespace Lunate.Tui.Tests;

public sealed class UnixRawModeTests
{
    // cfmakeraw's input set, spelled per the platform headers; only IXON differs
    // (Darwin: 0x200 in XNU bsd/sys/termios.h; Linux: 0x400 in glibc
    // bits/termios-c_iflag.h). The applied mask must clear all of these so CR
    // reaches the decoder as Enter instead of being translated to LF.
    private const uint IgnBrk = 0x0000_0001;
    private const uint BrkInt = 0x0000_0002;
    private const uint ParMrk = 0x0000_0008;
    private const uint IStrip = 0x0000_0020;
    private const uint InlCr = 0x0000_0040;
    private const uint IgnCr = 0x0000_0080;
    private const uint IcrNl = 0x0000_0100;
    private const uint IxOnMacOs = 0x0000_0200;
    private const uint IxOnLinux = 0x0000_0400;

    private const uint MacOsClearMask =
        IgnBrk | BrkInt | ParMrk | IStrip | InlCr | IgnCr | IcrNl | IxOnMacOs;
    private const uint LinuxClearMask =
        IgnBrk | BrkInt | ParMrk | IStrip | InlCr | IgnCr | IcrNl | IxOnLinux;

    [Fact]
    public void Macos_layout_matches_the_xnu_termios_header()
    {
        var layout = TermiosLayout.MacOs;

        Assert.Equal(0, layout.IFlagOffset);
        Assert.Equal(8, layout.OFlagOffset);
        Assert.Equal(16, layout.CFlagOffset);
        Assert.Equal(24, layout.LFlagOffset);
        Assert.Equal(32, layout.CcBase);
        Assert.Equal(20, layout.CcCount);
        Assert.Equal(16, layout.VMinIndex);
        Assert.Equal(17, layout.VTimeIndex);
        Assert.Equal(48, layout.VMinOffset);
        Assert.Equal(49, layout.VTimeOffset);

        Assert.Equal(0x0000_0008u, layout.Echo);
        Assert.Equal(0x0000_0080u, layout.ISig);
        Assert.Equal(0x0000_0100u, layout.ICanon);
        Assert.Equal(0x0000_0400u, layout.IExten);
        Assert.Equal(MacOsClearMask, layout.IFlagClearMask);
        Assert.Equal(1, layout.InputFlush);
    }

    [Fact]
    public void Linux_layout_matches_the_glibc_termios_headers()
    {
        var layout = TermiosLayout.Linux;

        Assert.Equal(0, layout.IFlagOffset);
        Assert.Equal(4, layout.OFlagOffset);
        Assert.Equal(8, layout.CFlagOffset);
        Assert.Equal(12, layout.LFlagOffset);
        // c_cc follows the one-byte c_line, so it starts at 17 (VMIN 23, VTIME 22).
        Assert.Equal(17, layout.CcBase);
        Assert.Equal(32, layout.CcCount);
        Assert.Equal(6, layout.VMinIndex);
        Assert.Equal(5, layout.VTimeIndex);
        Assert.Equal(23, layout.VMinOffset);
        Assert.Equal(22, layout.VTimeOffset);

        Assert.Equal(0x0000_0008u, layout.Echo);
        Assert.Equal(0x0000_0001u, layout.ISig);
        Assert.Equal(0x0000_0002u, layout.ICanon);
        Assert.Equal(0x0000_8000u, layout.IExten);
        Assert.Equal(LinuxClearMask, layout.IFlagClearMask);
        Assert.Equal(0, layout.InputFlush);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyRaw_clears_the_cfmakeraw_set_and_sets_vmin_vtime(bool macOs)
    {
        var layout = macOs ? TermiosLayout.MacOs : TermiosLayout.Linux;
        var state = new byte[UnixRawMode.TermiosBufferSize];
        state.AsSpan(layout.IFlagOffset, 4).Fill(0xFF);
        state.AsSpan(layout.LFlagOffset, 4).Fill(0xFF);
        state[layout.VMinOffset] = 99;
        state[layout.VTimeOffset] = 99;

        layout.ApplyRaw(state);

        uint iflag = BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(layout.IFlagOffset));
        uint lflag = BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(layout.LFlagOffset));
        Assert.Equal(0u, iflag & layout.IFlagClearMask);
        Assert.NotEqual(0u, iflag);
        Assert.Equal(0u, lflag & (layout.Echo | layout.ICanon | layout.ISig | layout.IExten));
        Assert.NotEqual(0u, lflag);
        Assert.Equal(1, state[layout.VMinOffset]);
        Assert.Equal(0, state[layout.VTimeOffset]);
    }

    [Fact]
    public void Icrnl_is_cleared_on_both_platforms()
    {
        Assert.NotEqual(0u, TermiosLayout.MacOs.IFlagClearMask & IcrNl);
        Assert.NotEqual(0u, TermiosLayout.Linux.IFlagClearMask & IcrNl);
    }

    /// <summary>
    /// The pin that answers "is Ctrl+S still XOFF?": IXON is part of the applied clear mask on
    /// both platforms, so after <c>Enter</c> writes the attributes back, Ctrl+S cannot freeze the
    /// terminal's output and is delivered to the decoder as an ordinary character. The byte window
    /// filled with 1s is the stand-in for a tcgetattr buffer, and the assert is over the same
    /// buffer tcsetattr writes back (the real path in <see cref="UnixRawMode.Enter"/>).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyRaw_clears_ixon_so_ctrl_s_is_not_xoff(bool macOs)
    {
        TermiosLayout layout = macOs ? TermiosLayout.MacOs : TermiosLayout.Linux;
        uint ixon = macOs ? IxOnMacOs : IxOnLinux;
        var state = new byte[UnixRawMode.TermiosBufferSize];
        state.AsSpan(layout.IFlagOffset, 4).Fill(0xFF);

        layout.ApplyRaw(state);

        uint iflag = BinaryPrimitives.ReadUInt32LittleEndian(state.AsSpan(layout.IFlagOffset));
        Assert.Equal(0u, iflag & ixon);
    }

    [Fact]
    public void Current_picks_the_layout_for_the_running_platform()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(
            OperatingSystem.IsMacOS() ? TermiosLayout.MacOs : TermiosLayout.Linux,
            UnixRawMode.Current
        );
    }

    /// <summary>
    /// The restore writes the captured attributes back so the pty is exactly as the session found
    /// it. Darwin sets PENDIN on the tcsetattr that re-enables ICANON, and only a read or an input
    /// flush clears it; with no reader on this slave the flush is the only thing that can. This is
    /// a real pty, not a byte-window stand-in, so the kernel's own attribute handling is under test.
    /// </summary>
    [Fact]
    public void Enter_restores_a_pty_slave_to_its_captured_baseline()
    {
        Assert.SkipUnless(
            !OperatingSystem.IsWindows(),
            "the pty allocation and termios calls are POSIX-only."
        );

        int master = posix_openpt(O_Rdwr);
        Assert.True(master >= 0, "posix_openpt failed");
        int slave = -1;
        try
        {
            Assert.Equal(0, grantpt(master));
            Assert.Equal(0, unlockpt(master));
            slave = open(SlaveName(master), O_Rdwr);
            Assert.True(slave >= 0, "opening the pty slave failed");

            var baseline = new byte[UnixRawMode.TermiosBufferSize];
            Assert.Equal(0, tcgetattr(slave, baseline));

            using (UnixRawMode.Enter(slave)) { }

            var restored = new byte[UnixRawMode.TermiosBufferSize];
            Assert.Equal(0, tcgetattr(slave, restored));

            Assert.Equal(Convert.ToHexString(baseline), Convert.ToHexString(restored));
        }
        finally
        {
            if (slave >= 0)
            {
                close(slave);
            }

            close(master);
        }
    }

    private static string SlaveName(int master)
    {
        var buffer = new byte[256];
        Assert.Equal(0, ptsname_r(master, buffer, (nuint)buffer.Length));
        int length = Array.IndexOf(buffer, (byte)0);
        Assert.True(length > 0);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
    }

    private const int O_Rdwr = 2;

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_openpt(int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int grantpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlockpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ptsname_r(int fd, byte[] buffer, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string path, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, byte[] termios);
}
