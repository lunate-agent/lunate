using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The pass-through contract at the real spawn: the configured exact names reach the tool child on
/// POSIX and on Windows, a credential name the user did not list is still stripped, and a name whose
/// segment only resembles one (`GITLAB_TOKENIZE_TIMEOUT`, not `TOKEN`) survives. The parent set is
/// handed in through the request, so the test never mutates the process environment.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class ChildProcessToolEnvironmentTests
{
    private const string PassedThroughToken = "sk-passthrough";
    private const string StrippedCredential = "alias-stripped";
    private const string LookAlike = "user-owned";
    private const string UserToken = "project-token";

    [Fact]
    public async Task A_passed_through_name_reaches_the_posix_tool_child()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("the POSIX spawn path is pinned on POSIX; the Windows test covers its own");
        }

        string stdout = await RunAsync(
            "/bin/bash",
            [
                "-c",
                "printf '%s|%s|%s|%s' \"${OPENAI_API_KEY:-missing}\" "
                    + "\"${GEMINI_API_KEY:-stripped}\" "
                    + "\"${GITLAB_TOKENIZE_TIMEOUT:-missing}\" "
                    + "\"${MY_PROJECT_TOKEN:-missing}\"",
            ]
        );

        AssertPassThrough(stdout);
    }

    [Fact]
    public async Task A_passed_through_name_reaches_the_windows_tool_child()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip(
                "the Windows spawn path is pinned on Windows; the POSIX test covers its own"
            );
        }

        string stdout = await RunAsync(
            Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            [
                "/d",
                "/c",
                // cmd leaves an undefined %VAR% literal on the command line, so absence is
                // asked with `if defined` instead of printing the variable and expecting empty.
                "echo [%OPENAI_API_KEY%][%GITLAB_TOKENIZE_TIMEOUT%][%MY_PROJECT_TOKEN%]"
                    + "& if defined GEMINI_API_KEY (echo leaked) else (echo stripped)",
            ]
        );

        Assert.Equal($"[{PassedThroughToken}][{LookAlike}][{UserToken}]\nstripped", stdout.Trim());
    }

    private static async Task<string> RunAsync(string fileName, IReadOnlyList<string> arguments)
    {
        ToolEnvironmentPassthrough.Configure(["OPENAI_API_KEY", "MY_PROJECT_TOKEN"]);
        try
        {
            using var temp = new TempDirectory();
            var request = new ProcessRunRequest(
                fileName,
                arguments,
                temp.Root,
                Environment: ParentEnvironment()
            );
            using Lunate.Coding.ChildProcess child = Lunate.Coding.ChildProcess.Start(request);
            string stdout = await child.StandardOutput.ReadToEndAsync(
                TestContext.Current.CancellationToken
            );
            string stderr = await child.StandardError.ReadToEndAsync(
                TestContext.Current.CancellationToken
            );
            int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

            Assert.True(exit == 0, $"the child exited {exit}: {stderr}");
            return stdout.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    private static void AssertPassThrough(string stdout)
    {
        Assert.Equal($"{PassedThroughToken}|stripped|{LookAlike}|{UserToken}", stdout.Trim());
    }

    /// <summary>
    /// The parent set the request inherits: the configured credential pair, the look-alike, the
    /// user's token, and the platform names a child needs to start a shell.
    /// </summary>
    private static Dictionary<string, string> ParentEnvironment()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OPENAI_API_KEY"] = PassedThroughToken,
            ["GEMINI_API_KEY"] = StrippedCredential,
            ["GITLAB_TOKENIZE_TIMEOUT"] = LookAlike,
            ["MY_PROJECT_TOKEN"] = UserToken,
        };

        if (OperatingSystem.IsWindows())
        {
            parent["SystemRoot"] =
                Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            parent["ComSpec"] =
                Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
            parent["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        }
        else
        {
            parent["PATH"] = "/usr/bin:/bin";
        }

        return parent;
    }
}
