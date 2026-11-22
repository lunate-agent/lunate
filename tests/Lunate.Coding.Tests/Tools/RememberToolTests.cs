using System.Text;
using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The core <c>remember</c> tool: one note appended to the repository root's LUNATE.md, existing
/// bytes untouched, one explicit budget and no caller-chosen path.
/// </summary>
public sealed class RememberToolTests
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    [Fact]
    public void Tool_shape_is_pinned()
    {
        using var temp = new TempDirectory();
        var tool = new RememberTool(new Workspace(temp.Root));

        Assert.Equal("remember", tool.Name);
        Assert.Equal(ToolRisk.Write, tool.Risk);
        JsonElement schema = tool.ParametersSchema;
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), element =>
            element.GetString() == "note"
        );
    }

    [Fact]
    public async Task The_first_note_creates_the_project_file()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);

        ToolResult result = await ExecuteAsync(workspace, """{"note":"builds with .NET 11"}""");

        Assert.False(result.IsError);
        Assert.Contains(ProjectNotes.FileName, result.Output, StringComparison.Ordinal);
        Assert.Contains("created", result.Output, StringComparison.Ordinal);
        Assert.Contains("8,000", result.Output, StringComparison.Ordinal);
        Assert.Equal(
            "# Project notes\n\n- builds with .NET 11\n",
            File.ReadAllText(temp.File(ProjectNotes.FileName)).ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public async Task Appending_keeps_the_existing_bytes()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);
        File.WriteAllText(temp.File(ProjectNotes.FileName), "# My notes\n\n- existing\n");
        byte[] before = File.ReadAllBytes(temp.File(ProjectNotes.FileName));

        ToolResult result = await ExecuteAsync(workspace, """{"note":"another fact"}""");

        Assert.False(result.IsError);
        byte[] after = File.ReadAllBytes(temp.File(ProjectNotes.FileName));
        Assert.Equal(before, after[..before.Length]);
        Assert.Equal(
            "# My notes\n\n- existing\n- another fact\n",
            Encoding.UTF8.GetString(after).ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public async Task A_missing_final_newline_is_repaired_without_rewriting_a_line()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);
        File.WriteAllText(temp.File(ProjectNotes.FileName), "- existing");

        ToolResult result = await ExecuteAsync(workspace, """{"note":"another fact"}""");

        Assert.False(result.IsError);
        Assert.Equal(
            "- existing\n- another fact\n",
            File.ReadAllText(temp.File(ProjectNotes.FileName)).ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public async Task An_existing_CRLF_file_with_a_BOM_keeps_both()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);
        File.WriteAllBytes(
            temp.File(ProjectNotes.FileName),
            [.. Bom, .. Encoding.UTF8.GetBytes("one\r\n")]
        );

        ToolResult result = await ExecuteAsync(workspace, """{"note":"two"}""");

        Assert.False(result.IsError);
        Assert.Equal(
            [.. Bom, .. Encoding.UTF8.GetBytes("one\r\n- two\r\n")],
            File.ReadAllBytes(temp.File(ProjectNotes.FileName))
        );
    }

    [Fact]
    public async Task The_budget_refuses_an_append_and_leaves_the_file_unchanged()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);
        File.WriteAllText(
            temp.File(ProjectNotes.FileName),
            new string('a', ProjectNotes.MaxCharacters)
        );
        byte[] before = File.ReadAllBytes(temp.File(ProjectNotes.FileName));

        ToolResult result = await ExecuteAsync(workspace, """{"note":"one too many"}""");

        Assert.True(result.IsError);
        Assert.Contains("8,000", result.Output, StringComparison.Ordinal);
        Assert.Contains("LUNATE.md", result.Output, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(temp.File(ProjectNotes.FileName)));
    }

    [Theory]
    [InlineData("""{"note":"   "}""")]
    [InlineData("""{"note":"one\ntwo"}""")]
    [InlineData("""{"note":"one","path":"other.md"}""")]
    [InlineData("""{}""")]
    public async Task Invalid_notes_are_refused_without_a_write(string arguments)
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);

        ToolResult result = await ExecuteAsync(workspace, arguments);

        Assert.True(result.IsError);
        Assert.False(File.Exists(temp.File(ProjectNotes.FileName)));
    }

    [Fact]
    public async Task A_note_that_cannot_fit_is_refused_without_a_write()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);

        ToolResult result = await ExecuteAsync(
            workspace,
            JsonSerializer.Serialize(new { note = new string('a', ProjectNotes.MaxCharacters) })
        );

        Assert.True(result.IsError);
        Assert.False(File.Exists(temp.File(ProjectNotes.FileName)));
    }

    [Fact]
    public async Task A_nested_launch_writes_the_repository_root_file_only()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.File(".git"));
        string nested = temp.File("src/App");
        Directory.CreateDirectory(nested);
        var workspace = new Workspace(nested);

        ToolResult result = await ExecuteAsync(workspace, """{"note":"root fact"}""");

        Assert.False(result.IsError);
        Assert.True(File.Exists(temp.File(ProjectNotes.FileName)));
        Assert.False(File.Exists(Path.Combine(nested, ProjectNotes.FileName)));
    }

    [Fact]
    public async Task A_successful_append_emits_the_file_change()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);
        var sink = new RecordingFileChangeSink();

        ToolResult created = await ExecuteAsync(workspace, """{"note":"one"}""", sink);
        ToolResult modified = await ExecuteAsync(workspace, """{"note":"two"}""", sink);

        Assert.False(created.IsError);
        Assert.False(modified.IsError);
        Assert.Collection(
            sink.Events,
            change =>
            {
                Assert.Equal(FileChangeKind.Created, change.Kind);
                Assert.Equal(ProjectNotes.FileName, change.RelativePath);
                Assert.Equal("run_test", change.RunId);
            },
            change =>
            {
                Assert.Equal(FileChangeKind.Modified, change.Kind);
                Assert.Equal(ProjectNotes.FileName, change.RelativePath);
            }
        );
    }

    private static Task<ToolResult> ExecuteAsync(
        Workspace workspace,
        string arguments,
        IFileChangeSink? sink = null
    ) =>
        new RememberTool(workspace).ExecuteAsync(
            JsonDocument.Parse(arguments).RootElement,
            new ToolContext(workspace.WorktreeRoot, new NullAgentEvents())
            {
                RunId = "run_test",
                FileChanges = sink ?? NullFileChangeSink.Instance,
            },
            CancellationToken.None
        );

    private sealed class RecordingFileChangeSink : IFileChangeSink
    {
        public List<FileChangeEvent> Events { get; } = [];

        public void Notify(FileChangeEvent change) => Events.Add(change);
    }
}
