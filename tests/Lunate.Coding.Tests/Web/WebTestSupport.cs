using System.Text;
using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

internal static class WebTestSupport
{
    internal static string Fixture(string name) =>
        File.ReadAllText(
            Path.Combine(Lunate.TestSupport.RepositoryRoot.Find(), "tests", "fixtures", "web", name)
        );

    internal static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    internal static int ByteCount(string value) => Encoding.UTF8.GetByteCount(value);

    internal static ToolContext Context() => new("unused", new NullAgentEvents());
}
