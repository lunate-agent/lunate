using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace ObserverExtension;

/// <summary>
/// The observing half of the extension surface, and nothing else: this extension watches a session
/// through <see cref="IExtensionContext.Events"/> and registers no hook handler, so a test that runs
/// it proves an observer sees a turn from start to end without being able to intercept it
/// (ADR-0040).
/// </summary>
public sealed class ObserverExtension : IExtension;

public sealed class ObserverExtensionFactory : IExtensionFactory
{
    public IExtension Create(IExtensionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? marker =
            context.Settings.TryGet("markerPath", out JsonElement path)
            && path.ValueKind == JsonValueKind.String
                ? path.GetString()
                : null;
        context.Events.Subscribe(new MarkerObserver(marker));
        return new ObserverExtension();
    }
}

/// <summary>
/// Appends one line per observed event and one on completion, so the test reads the turn's shape
/// from disk after the process has said the run is over.
/// </summary>
internal sealed class MarkerObserver(string? marker) : IObserver<AgentEvent>
{
    private readonly object _gate = new();

    public void OnNext(AgentEvent value) => Append(value.Value?.GetType().Name ?? "unknown");

    public void OnError(Exception error) => Append("error: " + error.GetType().Name);

    public void OnCompleted() => Append("completed");

    private void Append(string line)
    {
        if (string.IsNullOrEmpty(marker))
        {
            return;
        }

        lock (_gate)
        {
            File.AppendAllText(marker, line + "\n");
        }
    }
}
