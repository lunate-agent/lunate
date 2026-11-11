using System.Text.Json;
using Lunate.Agent;

namespace S7;

/// <summary>The event sink every tool context needs; the spike records and never fails.</summary>
internal sealed class RecordingEvents : IAgentEvents
{
    internal List<AgentEvent> Events { get; } = [];

    public void Emit(AgentEvent agentEvent) => Events.Add(agentEvent);
}

/// <summary>
/// Records what the core's file-change path publishes. This is how the spike sees the
/// <c>bash</c> change scan (host-side <c>git</c> over the mounted workspace) without a session.
/// </summary>
internal sealed class RecordingSink : IFileChangeSink
{
    internal List<FileChangeEvent> Changes { get; } = [];

    public void Notify(FileChangeEvent change) => Changes.Add(change);

    internal string Describe() =>
        Changes.Count == 0
            ? "(no change published)"
            : string.Join(
                "; ",
                Changes.Select(change => $"{change.Source}:{change.Kind}:{change.RelativePath}")
            );
}

/// <summary>Builds the context a tool call runs in: no run, no nested tools, a recording sink.</summary>
internal static class Tooling
{
    internal static ToolContext Context(SpikeOptions options, RecordingSink sink) =>
        new(options.Workspace, new RecordingEvents())
        {
            RunId = "s7-spike",
            CallId = "s7-spike/1",
            FileChanges = sink,
        };

    internal static JsonElement Args(string path, string? content = null)
    {
        var json = new Dictionary<string, string> { ["path"] = path };
        if (content is not null)
        {
            json["content"] = content;
        }

        return JsonDocument.Parse(JsonSerializer.Serialize(json)).RootElement.Clone();
    }

    internal static JsonElement BashArgs(string command) =>
        JsonDocument
            .Parse(
                JsonSerializer.Serialize(new Dictionary<string, string> { ["command"] = command })
            )
            .RootElement.Clone();
}
