using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Lunate.Agent;
using Lunate.Ai;
using Lunate.Extensibility.Testing;
using Lunate.TestSupport;

namespace Lunate.Extensibility.Testing.Tests;

/// <summary>
/// #464: the template README says "copy it, rename it, replace the handlers", and a copy outside
/// this repository must keep that promise. A copy does not see <c>Directory.Build.props</c>, so
/// the template's own project file carries the properties every SDK project needs, restores the
/// contract pair as packages (<c>LunateUseNuGetContracts=true</c>) from a feed of freshly packed
/// packages, and the built copy loads through the real loader and replays the fixture.
/// </summary>
/// <remarks>
/// This exercises the prove path outside the repository; the verify gate keeps proving the same
/// package shape in-repository, and the solution keeps compiling the template projects.
/// </remarks>
public sealed class ExtensionTemplateCopyEndToEndTests
{
    [Fact]
    public async Task Template_copy_builds_and_loads_outside_the_repository()
    {
        string repository = RepositoryRoot.Find();
        using var temp = new TempDirectory();

        string feed = temp.Subdirectory("contract-feed");
        // Every child this test starts gets the folders and the packages folder it would otherwise
        // share with the rest of the shard pinned to this test (see Dotnet): a shard runs its
        // assemblies beside each other, and this test's pack, restore and build must not read or
        // write another assembly's cache.
        string packages = temp.Subdirectory("packages");
        string child = temp.Subdirectory("child-dotnet");
        Pack(repository, feed, packages, child);
        AssertContractPackageTargetsNet11(feed);

        // The copy restores into a packages folder and through a NuGet configuration of the run's
        // own. A runner's folder can hold the published contract at this version, and NuGet binds an
        // installed copy before the feed this test just packed. `verify (macos-latest, 2/2)` did
        // exactly that, built the copy against 1.16.0 outside Directory.Build.props (where CS8509 is
        // only a warning) and failed at load with a TypeLoadException on HandleAsync instead of
        // proving this checkout's shape.
        // The sources are the copy's own configuration file (see WriteNuGetConfig): run 44033633019,
        // the red `verify (windows-latest, 2/4)` this test owns, failed on the three legs that
        // restore the template against the feed they had just packed -- and on no leg that does not.
        string config = WriteNuGetConfig(child, feed);

        string copy = CopyTemplate(repository, temp.Subdirectory("my-extension"));
        // Both projects a copy carries: the extension a host loads and the test project the
        // README keeps as the place where behavior is proven.
        RestoreAndBuild(
            Path.Combine(copy, "TemplateExtension", "TemplateExtension.csproj"),
            config,
            packages,
            child,
            ReleaseVersions.Contract
        );
        RestoreAndBuild(
            Path.Combine(copy, "TemplateExtension.Tests", "TemplateExtension.Tests.csproj"),
            config,
            packages,
            child,
            ReleaseVersions.Contract,
            ReleaseVersions.Preview
        );

        string extensionOutput = Path.Combine(
            copy,
            "TemplateExtension",
            "bin",
            Configuration,
            TargetFramework
        );
        using var hostTemp = new TempDirectory();
        var approver = new ScriptedApprover(true);
        await using var host = new ExtensionTestHost(
            new ExtensionTestHostOptions
            {
                TempDirectory = hostTemp.Root,
                ExtensionId = "template",
                ExtensionDirectory = extensionOutput,
                Approver = approver,
            }
        );
        host.Tools.Add(new EchoTool());

        ExtensionRunResult result = await host.RunAsync(
            new ReplayChatClient(FixturePath(repository)),
            "call the echo tool",
            TestContext.Current.CancellationToken
        );
        await host.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(host.Log.Entries, entry =>
            entry.Message.Contains("template", StringComparison.Ordinal)
            && entry.Message.Contains("tool-calling", StringComparison.Ordinal)
        );
        Assert.Single(host.Log.Entries, entry =>
            entry.Message.Contains("service started", StringComparison.Ordinal)
        );
        Assert.Single(host.Log.Entries, entry =>
            entry.Message.Contains("service stopped", StringComparison.Ordinal)
        );
        Assert.Equal(1, approver.Calls);
        Assert.Equal(1, approver.Approvals);
        Assert.Contains(result.Events.OfType<ToolCallResult>(), toolResult => !toolResult.IsError);
        Assert.Contains(result.Events.OfType<RunFinished>(), finished =>
            finished.StopReason == StopReasons.Stop
        );
    }

    /// <summary>
    /// The configuration this test restores through is XML, and a feed path is not: a temp directory
    /// may hold <c>&amp;</c> (Windows allows it in a profile path) or <c>&lt;</c> (valid on Unix),
    /// and written raw the file is malformed XML, which makes the restore fail before it proves
    /// anything (#936). Written as an XML attribute, the document parses and reads the path back
    /// unchanged.
    /// </summary>
    [Fact]
    public void The_configuration_escapes_a_feed_path_xml_cannot_carry_raw()
    {
        using var temp = new TempDirectory();
        string feed = Path.Combine(temp.Root, "a&b<c>d\"e'f");

        string config = WriteNuGetConfig(temp.Root, feed);

        XDocument document = XDocument.Load(config);
        string? mapped = document
            .Element("configuration")
            ?.Element("packageSources")
            ?.Elements("add")
            .FirstOrDefault(element => (string?)element.Attribute("key") == "packed")
            ?.Attribute("value")
            ?.Value;

        Assert.Equal(feed, mapped);
        Assert.Contains("&amp;", File.ReadAllText(config), StringComparison.Ordinal);
    }

    private const string Configuration = "Release";
    private const string TargetFramework = "net11.0";
    private const string NuGetOrg = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// The contract package carries one <c>net11.0</c> asset and nothing else (ADR-0041): the
    /// repository does not multi-target, so a <c>lib/net10.0</c> entry would be a target framework
    /// the build no longer proves.
    /// </summary>
    private static void AssertContractPackageTargetsNet11(string feed)
    {
        string package = Assert.Single(Directory.GetFiles(feed, "Lunate.Contracts.*.nupkg"));
        using ZipArchive archive = ZipFile.OpenRead(package);
        string[] libraries =
        [
            .. archive
                .Entries.Select(entry => entry.FullName)
                .Where(name => name.StartsWith("lib/", StringComparison.Ordinal)),
        ];
        Assert.Contains("lib/net11.0/Lunate.Contracts.dll", libraries);
        Assert.All(libraries, name =>
            Assert.StartsWith("lib/net11.0/", name, StringComparison.Ordinal)
        );
    }

    private static void Pack(string repository, string feed, string packages, string child)
    {
        // Packing the repository's own projects restores and builds them, and that writes restore
        // state (obj/project.assets.json) beside every project of the graph. This test pins its
        // children to a packages folder inside its own temp directory, so letting that state land
        // in the worktree would leave the next build there reading a folder the test deleted:
        // `--artifacts-path` keeps the whole packed graph's obj, bin and pack output under this
        // test's folder instead, and the worktree is left exactly as the gate built it.
        string artifacts = Path.Combine(child, "artifacts");
        string[] packables =
        [
            "src/Lunate.Contracts/Lunate.Contracts.csproj",
            "src/Lunate.Decisions.Model/Lunate.Decisions.Model.csproj",
            "src/Lunate.Extensibility.Abstractions/Lunate.Extensibility.Abstractions.csproj",
            "src/Lunate.Agent/Lunate.Agent.csproj",
            "src/Lunate.Extensibility.Testing/Lunate.Extensibility.Testing.csproj",
        ];
        foreach (string project in packables)
        {
            Dotnet(
                $"""pack "{Path.Combine(repository, project)}" -c {Configuration} -o "{feed}" """
                    + $"""--artifacts-path "{artifacts}" -maxcpucount:1 --nologo -nodeReuse:false""",
                packages,
                child
            );
        }
    }

    private static void RestoreAndBuild(
        string project,
        string config,
        string packages,
        string child,
        string contractVersion,
        string? previewVersion = null
    )
    {
        string testing = previewVersion is null
            ? string.Empty
            : $" -p:LunateTestingVersion={previewVersion}";
        string properties =
            $"""-p:LunateUseNuGetContracts=true -p:LunateContractsVersion={contractVersion}{testing}""";
        Dotnet(
            $"""restore "{project}" -p:Configuration={Configuration} {properties} """
                + $"""--configfile "{config}" --packages "{packages}" """
                + $"""-maxcpucount:1 --nologo -nodeReuse:false""",
            packages,
            child
        );
        Dotnet(
            $"""build "{project}" -c {Configuration} {properties} --no-restore """
                + $"""-maxcpucount:1 --nologo -nodeReuse:false""",
            packages,
            child
        );
    }

    /// <summary>
    /// Runs one child <c>dotnet</c> with every machine-wide folder and process it could share with
    /// another assembly of the same shard pinned to this test: the CLI home, the packages folder,
    /// the NuGet HTTP cache, the temp directory (NuGet's scratch files and MSBuild's response files
    /// live there) and the long-lived MSBuild server. <c>FixtureRestore</c> pins the same set for
    /// the gate's fixture restores, and for the same reason: a child here restores the repository
    /// itself, so without <paramref name="packages"/> it would fill the runner's global packages
    /// folder and read whatever an earlier job left there. That folder is the one the copy's
    /// restore names as <c>--packages</c>.
    /// </summary>
    private static void Dotnet(string arguments, string packages, string child)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.EnvironmentVariables["DOTNET_CLI_HOME"] = Subdirectory(child, "home");
        start.EnvironmentVariables["NUGET_PACKAGES"] = packages;
        start.EnvironmentVariables["NUGET_HTTP_CACHE_PATH"] = Subdirectory(child, "nuget-http");
        string tmp = Subdirectory(child, "tmp");
        start.EnvironmentVariables["TMPDIR"] = tmp;
        start.EnvironmentVariables["TMP"] = tmp;
        start.EnvironmentVariables["TEMP"] = tmp;

        start.EnvironmentVariables["MSBUILDDISABLENODEREUSE"] = "1";
        start.EnvironmentVariables["MSBUILDUSESERVER"] = "0";
        start.EnvironmentVariables["DOTNET_CLI_USE_MSBUILD_SERVER"] = "false";
        start.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.EnvironmentVariables["DOTNET_NOLOGO"] = "1";
        start.EnvironmentVariables["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        var buffer = new StringBuilder();
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                lock (buffer)
                {
                    _ = buffer.AppendLine(args.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                lock (buffer)
                {
                    _ = buffer.AppendLine(args.Data);
                }
            }
        };
        Assert.True(process.Start(), $"dotnet did not start: {arguments}");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Assert.True(
            process.WaitForExit(300_000),
            $"dotnet did not finish within five minutes: {arguments}"
        );
        Assert.True(process.ExitCode == 0, $"dotnet failed ({process.ExitCode}):\n{buffer}");
    }

    private static string Subdirectory(string parent, string name)
    {
        string path = Path.Combine(parent, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// The NuGet configuration the copy restores through: the packed feed as the only source of a
    /// <c>Lunate</c> package, nuget.org for the test SDK the copy's test project needs.
    /// </summary>
    /// <remarks>
    /// The copy pins the same contract version the release publishes, so nuget.org also serves a
    /// <c>Lunate.Contracts</c> of that version — with an earlier commit's content. Naming the packed
    /// feed first is an ordering rule, not a guarantee, and a runner whose cache is warm can answer
    /// with the released copy instead; <c>packageSourceMapping</c> makes the packed feed the only
    /// source a <c>Lunate</c> package may come from, so neither ordering nor a cache can decide which
    /// shape this test proved. Parity with the verify gate's roundtrip.
    /// </remarks>
    private static string WriteNuGetConfig(string root, string feed)
    {
        string path = Path.Combine(root, "nuget.config");
        File.WriteAllText(
            path,
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="packed" value="{XmlAttribute(feed)}" />
                <add key="nuget.org" value="{NuGetOrg}" />
              </packageSources>
              <packageSourceMapping>
                <packageSource key="packed">
                  <package pattern="Lunate.*" />
                </packageSource>
                <packageSource key="nuget.org">
                  <package pattern="*" />
                </packageSource>
              </packageSourceMapping>
            </configuration>
            """,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        );
        return path;
    }

    /// <summary>
    /// XML attribute encoding of the five characters an attribute cannot carry raw, <c>&amp;</c>
    /// first so the entities the others write survive. Parity with <c>xml_attribute</c> in
    /// <c>scripts/verify.sh</c> and <c>ConvertTo-XmlAttribute</c> in <c>scripts/verify.ps1</c>.
    /// </summary>
    private static string XmlAttribute(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string CopyTemplate(string repository, string destination)
    {
        CopyDirectory(
            Path.Combine(repository, "templates", "extension"),
            destination,
            skip: ["bin", "obj"]
        );
        return destination;
    }

    private static void CopyDirectory(string source, string destination, HashSet<string> skip)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            if (!skip.Contains(Path.GetFileName(directory)))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)),
                    skip
                );
            }
        }
    }

    private static string FixturePath(string repository) =>
        Path.Combine(
            repository,
            "tests",
            "fixtures",
            "streams",
            "extension-template-tool-call.jsonl"
        );

    /// <summary>The agent-side echo tool the template's fixture replay calls through the copy.</summary>
    private sealed class EchoTool : ITool
    {
        public string Name => "echo";

        public string Description => "Echoes the given text back.";

        public JsonElement ParametersSchema { get; } =
            JsonDocument
                .Parse(
                    """{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}"""
                )
                .RootElement.Clone();

        public Task<ToolResult> ExecuteAsync(
            JsonElement args,
            ToolContext ctx,
            CancellationToken ct
        )
        {
            string text = args.TryGetProperty("text", out JsonElement value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
            return Task.FromResult(new ToolResult($"echo: {text}", IsError: false));
        }
    }
}
