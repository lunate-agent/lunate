using Xunit;

namespace Lunate.Coding.Tests;

/// <summary>
/// The wizard's interactive fixtures drive a scripted POSIX terminal; Windows CI has no pty and
/// would wait forever on the session's shutdown. Product code remains cross-platform.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PosixFactAttribute : FactAttribute
{
    public PosixFactAttribute(
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
        : base(file, line)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "The interactive wizard fixture needs a POSIX terminal.";
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PosixTheoryAttribute : TheoryAttribute
{
    public PosixTheoryAttribute(
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
        : base(file, line)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "The interactive wizard fixture needs a POSIX terminal.";
        }
    }
}
