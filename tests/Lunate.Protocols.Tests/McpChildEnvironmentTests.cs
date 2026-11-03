using System.Globalization;
using System.Text.Json;
using Lunate.Agent;
using Lunate.TestSupport;

namespace Lunate.Protocols.Tests;

/// <summary>
/// #4472 — a stdio MCP server receives only the scrubber's base set plus its own declared
/// <c>env</c> map, nothing inherited beyond that. These tests spawn the shared TestMcpServer for
/// real and read the child's own environment report; the variables are set on the real process
/// environment (declared and undeclared credentials alike) and restored afterwards.
/// </summary>
public sealed class McpChildEnvironmentTests
{
    private const string AnthropicVariable = "ANTHROPIC_API_KEY";
    private const string ParentFlagVariable = "LUNATE_TEST_PARENT_FLAG";

    private static readonly string AnthropKeyValue = "sk-ant-fixture-9876543210";
    private static readonly string ParentFlagValue = "parent-flag-visible";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_undeclared_credential_is_absent_and_a_declared_one_is_present()
    {
        using var temp = new TempDirectory();
        IDisposable restore = SetTestEnvironment(
            (AnthropicVariable, AnthropKeyValue),
            (ParentFlagVariable, ParentFlagValue)
        );
        try
        {
            await using var host = new McpServerHost(
                EnvServerOptions(
                    temp.File("marker.txt"),
                    declaredEnvironment: new Dictionary<string, string>
                    {
                        [AnthropicVariable] = AnthropKeyValue,
                    }
                )
            );

            string[] names = await NamesAsync(host);

            // The server's own env map is the pass-through: the declared name receives its value.
            Assert.Contains(AnthropicVariable, names);
            Assert.Equal(AnthropKeyValue, await ValueAsync(host, AnthropicVariable));

            // Nothing else inherits: the undeclared credential and the undeclared parent flag are
            // absent whatever value they carry.
            Assert.DoesNotContain(ParentFlagVariable, names);
        }
        finally
        {
            restore.Dispose();
        }
    }

    [Fact]
    public async Task The_child_sees_the_base_set_and_nothing_else()
    {
        using var temp = new TempDirectory();
        IDisposable restore = SetTestEnvironment(
            (AnthropicVariable, AnthropKeyValue),
            (ParentFlagVariable, ParentFlagValue)
        );
        try
        {
            await using var host = new McpServerHost(EnvServerOptions(temp.File("marker.txt")));

            string[] names = await NamesAsync(host);

            // PATH reaches the child with the parent's value, so the child can start commands.
            Assert.Equal(
                Environment.GetEnvironmentVariable("PATH"),
                await ValueAsync(host, "PATH")
            );

            // The base set never carries the parent's other variables, credentials included: the
            // undeclared names are absent without any withheld list being wired.
            Assert.DoesNotContain(AnthropicVariable, names);
            Assert.DoesNotContain(ParentFlagVariable, names);
        }
        finally
        {
            restore.Dispose();
        }
    }

    /// <summary>The env-reporting options: the marker and the tool gate are declared.</summary>
    private static McpServerOptions EnvServerOptions(
        string markerPath,
        IReadOnlyDictionary<string, string>? declaredEnvironment = null
    )
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal)
        {
            ["LUNATE_TEST_MCP_MARKER"] = markerPath,
            ["LUNATE_TEST_MCP_ENV_TOOL"] = "1",
        };
        foreach (
            (string name, string value) in declaredEnvironment ?? new Dictionary<string, string>()
        )
        {
            environment[name] = value;
        }

        return new McpServerOptions(
            Name: "server",
            Command: TestMcpServer.DotnetPath,
            Arguments: ["exec", TestMcpServer.ServerDll],
            Environment: environment
        );
    }

    private static async Task<string[]> NamesAsync(McpServerHost host)
    {
        ToolResult result = await ExecuteAsync(host, "env_names");
        Assert.False(result.IsError);
        return (
            JsonSerializer.Deserialize<JsonElement>(result.Output).Deserialize<string[]>()
            ?? throw new InvalidOperationException("env_names returned no array")
        );
    }

    private static async Task<string> ValueAsync(McpServerHost host, string name) =>
        (await ExecuteAsync(host, "env_value", name)).Output;

    private static async Task<ToolResult> ExecuteAsync(
        McpServerHost host,
        string tool,
        string? argument = null
    )
    {
        IReadOnlyList<ITool> tools = await host.GetToolsAsync(Ct);
        JsonElement arguments = argument is null
            ? JsonSerializer.SerializeToElement(new { })
            : JsonSerializer.SerializeToElement(new { name = argument });
        return await tools
            .Single(candidate => candidate.Name == "server__" + tool)
            .ExecuteAsync(arguments, McpTestEnvironment.Context, Ct);
    }

    private static IDisposable SetTestEnvironment(params (string Name, string? Value)[] variables)
    {
        (string Name, string? Previous)[] original = variables
            .Select(entry => (entry.Name, Environment.GetEnvironmentVariable(entry.Name)))
            .ToArray();
        foreach ((string name, string? value) in variables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        return new Restorer(original);
    }

    private sealed class Restorer((string Name, string? Previous)[] original) : IDisposable
    {
        public void Dispose()
        {
            foreach ((string name, string? previous) in original)
            {
                Environment.SetEnvironmentVariable(name, previous);
            }
        }
    }
}
