namespace Lunate.Coding.Tests;

/// <summary>The frame waits the shell suite shares with the session tests.</summary>
internal static class InteractiveSessionCommandTests
{
    internal static async Task WaitForFrameAsync(
        InteractiveSessionHost host,
        string text,
        TimeSpan? timeout = null
    )
    {
        await host.WaitUntilAsync(
            () =>
            {
                host.Advance(33);
                return host.LastFrame.Contains(text, StringComparison.Ordinal);
            },
            timeout
        );
    }
}
