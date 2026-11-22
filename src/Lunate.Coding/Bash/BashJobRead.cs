namespace Lunate.Coding;

/// <summary>One <c>bash_output</c> read: the unread text, the state and the result's error flag.</summary>
internal sealed record BashJobRead(
    string Output,
    bool IsError,
    int? ExitCode,
    bool Running,
    bool TimedOut,
    bool Killed
);
