using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// The turn gate and wording: a notification is sent only for an enabled, known-unfocused turn at
/// or above the duration threshold, and its body names the outcome and the elapsed time. The sender
/// is called by the session inside its own failure-isolation boundary.
/// </summary>
internal static class TurnNotifier
{
    /// <summary>The longest failure message carried into a notification.</summary>
    internal const int MaxFailureLength = 200;

    internal static bool ShouldNotify(
        bool enabled,
        bool? focused,
        TimeSpan duration,
        TimeSpan minimumDuration
    ) => enabled && focused is false && duration >= minimumDuration;

    internal static NotificationMessage Message(
        string title,
        string? stopReason,
        string? errorMessage,
        TimeSpan duration
    ) =>
        new(
            string.IsNullOrWhiteSpace(title) ? "Lunate" : title,
            Outcome(stopReason, errorMessage) + " after " + RunLimits.FormatSeconds(duration)
        );

    private static string Outcome(string? stopReason, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return "failed: " + Bounded(errorMessage);
        }

        return stopReason switch
        {
            StopReasons.Stop => "finished",
            StopReasons.Length => "finished (answer cut at the length limit)",
            StopReasons.Cancelled => "cancelled",
            StopReasons.StepLimit => "stopped at the step limit",
            StopReasons.TokenLimit => "stopped at the token limit",
            StopReasons.CostLimit => "stopped at the cost limit",
            StopReasons.TimeLimit => "stopped at the time limit",
            null or "" => "finished",
            _ => "finished (" + stopReason + ")",
        };
    }

    private static string Bounded(string text)
    {
        string oneLine = string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        return oneLine.Length <= MaxFailureLength
            ? oneLine
            : oneLine[..(MaxFailureLength - 3)] + "...";
    }
}
