using System.Globalization;
using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility.Remote;

namespace Lunate.Extensibility.Testing;

/// <summary>
/// The observable-event conformance step (protocol version 3): when the child accepts event kinds,
/// the runner sends the probe events as real <c>event/&lt;kind&gt;</c> notifications and, when the
/// options name a probe command, invokes it and requires its output to name the kinds the child
/// received. Notifications are never acknowledged, so the probe command is the only receipt a
/// conformance run can observe; a child without one is sent-only and the step stays best effort.
/// </summary>
internal static class RemoteEventNotificationConformance
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>How long the probe command is retried for a child that reads notifications late.</summary>
    private static readonly TimeSpan ReceiptWindow = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan ReceiptRetry = TimeSpan.FromMilliseconds(40);

    public static async Task<RemoteConformanceStep> RunAsync(
        RemoteWire wire,
        RemoteConformanceOptions options,
        InitializeResult answer,
        CancellationToken cancellationToken
    )
    {
        if (answer.Events.Count == 0)
        {
            return new RemoteConformanceStep(
                "event notification",
                false,
                "the child accepts no event kinds; the observable projection was not exercised",
                Required: false
            );
        }

        IReadOnlyList<string> kinds =
        [
            .. options
                .ProbeEvents.Select(sample => RemoteEventJson.KindOf(sample.Event.Kind))
                .Where(kind => Accepted(answer.Events, kind))
                .Distinct(StringComparer.Ordinal),
        ];
        if (kinds.Count == 0)
        {
            return new RemoteConformanceStep(
                "event notification",
                false,
                "the child accepts none of the probe kinds; the observable projection was not exercised",
                Required: false
            );
        }

        try
        {
            // Writes are ordered on one wire, so every notification is read before the probe
            // command frame that follows it.
            foreach (RemoteEventSample sample in options.ProbeEvents)
            {
                string kind = RemoteEventJson.KindOf(sample.Event.Kind);
                if (!kinds.Contains(kind, StringComparer.Ordinal))
                {
                    continue;
                }

                using JsonDocument line = JsonDocument.Parse(RemoteEventJson.Serialize(sample.Event));
                await wire
                    .NotifyAsync(
                        RemoteProtocol.EventMethod(kind),
                        line.RootElement.Clone(),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(options.EventProbeCommand))
            {
                return new RemoteConformanceStep(
                    "event notification",
                    false,
                    Sent(kinds, "no EventProbeCommand is configured, so receipt was not asserted"),
                    Required: false
                );
            }

            CommandInvokeResult? reply = null;
            List<string> missing = [.. kinds];
            long deadline = Environment.TickCount64 + (long)ReceiptWindow.TotalMilliseconds;
            while (missing.Count > 0 && Environment.TickCount64 < deadline)
            {
                reply = await InvokeProbeCommandAsync(wire, options, cancellationToken)
                    .ConfigureAwait(false);
                missing =
                [
                    .. kinds.Where(kind =>
                        reply.Output is null
                        || !reply.Output.Contains(kind, StringComparison.Ordinal)
                    ),
                ];
                if (missing.Count > 0)
                {
                    // A child dispatches its read loop off the write path; give it one more turn.
                    await Task.Delay(ReceiptRetry, cancellationToken).ConfigureAwait(false);
                }
            }

            return new RemoteConformanceStep(
                "event notification",
                missing.Count == 0,
                missing.Count == 0
                    ? Sent(kinds, "the probe command named every kind")
                    : Sent(
                        kinds,
                        $"the probe command from the child never named: {string.Join(", ", missing)} (it said: {reply?.Output ?? "nothing"})"
                    )
            );
        }
        catch (Exception exception)
        {
            return new RemoteConformanceStep(
                "event notification",
                false,
                $"{exception.GetType().Name}: {exception.Message}"
            );
        }
    }

    private static async Task<CommandInvokeResult> InvokeProbeCommandAsync(
        RemoteWire wire,
        RemoteConformanceOptions options,
        CancellationToken cancellationToken
    )
    {
        JsonElement? replied = await wire.CallWithIdAsync(
                RemoteProtocol.CommandInvokeMethod,
                id =>
                    JsonSerializer.SerializeToElement(
                        new CommandInvokeRequest(options.EventProbeCommand!, string.Empty, id),
                        JsonOptions
                    ),
                options.Timeout,
                cancellationToken
            )
            .ConfigureAwait(false);
        return replied?.Deserialize<CommandInvokeResult>(JsonOptions)
            ?? throw new InvalidOperationException(
                $"the {RemoteProtocol.CommandInvokeMethod} reply was empty"
            );
    }

    private static string Sent(IReadOnlyList<string> kinds, string outcome) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"sent event/{string.Join(", event/", kinds)}; {outcome}"
        );

    private static bool Accepted(IReadOnlyList<string> accepted, string kind) =>
        accepted.Contains("*", StringComparer.Ordinal)
        || accepted.Contains(kind, StringComparer.Ordinal);
}
