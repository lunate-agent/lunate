using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility.Tests;

/// <summary>
/// The shared child-environment scrubber: exactly the base set plus pass-through plus explicit
/// values — a parent key absent from the parent is absent from the child, and nothing leaks.
/// </summary>
public sealed class EnvironmentScrubberTests
{
    private static readonly IReadOnlyDictionary<string, string> Parent = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin",
        ["PATHEXT"] = ".COM;.EXE;.BAT;.CMD",
        ["HOME"] = "/home/user",
        ["USERPROFILE"] = @"C:\Users\user",
        ["SystemRoot"] = @"C:\Windows",
        ["ComSpec"] = @"C:\Windows\System32\cmd.exe",
        ["TEMP"] = "/tmp",
        ["TMP"] = "/tmp",
        ["APPDATA"] = @"C:\Users\user\AppData\Roaming",
        ["LOCALAPPDATA"] = @"C:\Users\user\AppData\Local",
        ["LANG"] = "en_US.UTF-8",
        ["ANTHROPIC_API_KEY"] = "sk-secret",
        ["TMPDIR"] = "/var/tmp",
    };

    private static Func<string, string?> Read(IReadOnlyDictionary<string, string> source) =>
        name => source.TryGetValue(name, out string? value) ? value : null;

    [Fact]
    public void Windows_replaces_a_case_differing_name_instead_of_duplicating_it()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            read: name => name == "PATH" ? "/usr/bin" : null,
            set: new Dictionary<string, string> { ["Path"] = "over" }
        );

        if (OperatingSystem.IsWindows())
        {
            // Windows environment names are case-insensitive: the declared value replaces the
            // base one instead of coexisting with it.
            Assert.Single(environment);
            Assert.Equal("over", environment["PATH"]);
        }
        else
        {
            Assert.Equal(2, environment.Count);
        }
    }

    [Fact]
    public void The_base_set_is_present_and_nothing_else_inherits()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            Read(Parent)
        );

        Assert.Equal("/usr/bin", environment["PATH"]);
        Assert.Equal(".COM;.EXE;.BAT;.CMD", environment["PATHEXT"]);
        Assert.Equal("/home/user", environment["HOME"]);
        Assert.Equal(@"C:\Users\user", environment["USERPROFILE"]);
        Assert.Equal(@"C:\Windows", environment["SystemRoot"]);
        Assert.Equal(@"C:\Windows\System32\cmd.exe", environment["ComSpec"]);
        Assert.Equal("/tmp", environment["TEMP"]);
        Assert.Equal("/tmp", environment["TMP"]);
        Assert.Equal(@"C:\Users\user\AppData\Roaming", environment["APPDATA"]);
        Assert.Equal(@"C:\Users\user\AppData\Local", environment["LOCALAPPDATA"]);
        Assert.Equal("en_US.UTF-8", environment["LANG"]);
        Assert.Equal("/var/tmp", environment["TMPDIR"]);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", environment.Keys);
        Assert.Equal(EnvironmentScrubber.BaseNames.Count, environment.Count);
    }

    [Fact]
    public void A_passthrough_name_absent_from_the_parent_stays_absent()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            Read(Parent),
            passthrough: ["GITHUB_TOKEN", "HOME"]
        );

        Assert.DoesNotContain("GITHUB_TOKEN", environment.Keys);
        Assert.Equal("/home/user", environment["HOME"]);
    }

    [Fact]
    public void An_explicit_value_wins_over_the_parent()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            Read(Parent),
            set: new Dictionary<string, string> { ["LANG"] = "de_AT.UTF-8", ["EXTRA"] = "set" }
        );

        Assert.Equal("de_AT.UTF-8", environment["LANG"]);
        Assert.Equal("set", environment["EXTRA"]);
    }

    [Fact]
    public void Empty_base_values_are_skipped()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            Read(new Dictionary<string, string> { ["PATH"] = "", ["LANG"] = "C" })
        );

        Assert.DoesNotContain("PATH", environment.Keys);
        Assert.Equal("C", environment["LANG"]);
        Assert.Single(environment);
    }

    [Fact]
    public void The_inherited_policy_takes_the_whole_parent_environment()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            inherited: Parent
        );

        // Everything the parent holds, not the base-set allow-list: the tool child is the user's
        // shell, so a toolchain's own variable is the user's business.
        Assert.Equal(Parent.Count, environment.Count);
        Assert.Equal("sk-secret", environment["ANTHROPIC_API_KEY"]);
        Assert.Equal("/usr/bin", environment["PATH"]);
    }

    [Fact]
    public void The_inherited_policy_removes_every_stripped_name()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            strip: ["ANTHROPIC_API_KEY", "GEMINI_API_KEY"],
            inherited: Parent
        );

        Assert.DoesNotContain("ANTHROPIC_API_KEY", environment.Keys);
        Assert.DoesNotContain("GEMINI_API_KEY", environment.Keys);
        // The strip is a name list: an unrelated name survives whatever it is called.
        Assert.Equal("/usr/bin", environment["PATH"]);
        Assert.Equal("en_US.UTF-8", environment["LANG"]);
    }

    [Fact]
    public void A_stripped_name_cannot_be_put_back_by_a_declared_value()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            set: new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "re-added" },
            strip: ["ANTHROPIC_API_KEY"],
            inherited: Parent
        );

        // The strip is a deny for the whole child, so no spawn site can re-introduce a credential
        // by declaring it as an extra value.
        Assert.DoesNotContain("ANTHROPIC_API_KEY", environment.Keys);
    }

    [Fact]
    public void The_scrubbed_policy_ignores_the_strip()
    {
        // A strip list is an inherited-policy instrument; the scrubbed policy already cannot carry
        // a name the caller did not declare.
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            Read(Parent),
            strip: ["PATH"]
        );

        Assert.Equal("/usr/bin", environment["PATH"]);
    }
}
