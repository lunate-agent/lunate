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
        ["GITHUB_TOKEN"] = "ghp_parent",
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
            passthrough: ["NPM_TOKEN", "HOME"]
        );

        Assert.DoesNotContain("NPM_TOKEN", environment.Keys);
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
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["NODE_OPTIONS"] = "--max-old-space-size=4096",
            ["MY_BUILD_FLAG"] = "on",
        };

        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            inherited: parent
        );

        // Everything the parent holds, not the base-set allow-list: the tool child is the user's
        // shell, so a toolchain's own variable is the user's business. The only exception is a
        // credential-shaped name, which the shape rule withholds by default (#741).
        Assert.Equal(parent.Count, environment.Count);
        Assert.Equal("--max-old-space-size=4096", environment["NODE_OPTIONS"]);
        Assert.Equal("/usr/bin", environment["PATH"]);
    }

    [Fact]
    public void The_inherited_policy_withholds_a_credential_shaped_name_lunate_never_resolved()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["GITHUB_TOKEN"] = "ghp_ambient",
            ["NPM_TOKEN"] = "npm_ambient",
            ["AWS_SECRET_ACCESS_KEY"] = "aws-ambient",
            ["MY_TOOLCHAIN_FLAG"] = "on",
        };

        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            inherited: parent
        );

        // The strip closes the leak for the keys Lunate resolved; the shape rule closes it for the
        // credentials a developer machine holds and Lunate never heard of (#741).
        Assert.DoesNotContain("GITHUB_TOKEN", environment.Keys);
        Assert.DoesNotContain("NPM_TOKEN", environment.Keys);
        Assert.DoesNotContain("AWS_SECRET_ACCESS_KEY", environment.Keys);
        Assert.Equal("on", environment["MY_TOOLCHAIN_FLAG"]);
        Assert.Equal("/usr/bin", environment["PATH"]);
    }

    [Fact]
    public void The_shape_rule_is_a_word_match_not_a_substring_match()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MONKEY"] = "1",
            ["KEYBOARD_LAYOUT"] = "us",
            ["HOCKEY_POOL"] = "2",
            ["GITLAB_TOKENIZE_TIMEOUT"] = "30",
            ["OPENAI_BASE_URL"] = "https://gateway.example.test/v1",
        };

        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            inherited: parent
        );

        // A variable whose name merely contains a credential word is the user's own: withholding
        // it would break a build with no error message at all.
        Assert.Equal(parent.Count, environment.Count);
        Assert.Equal("us", environment["KEYBOARD_LAYOUT"]);
        Assert.Equal("30", environment["GITLAB_TOKENIZE_TIMEOUT"]);
        Assert.Equal("https://gateway.example.test/v1", environment["OPENAI_BASE_URL"]);
    }

    [Fact]
    public void The_shape_rule_is_case_insensitive()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["github_token"] = "ghp_ambient",
            ["Monkey"] = "survives",
        };

        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            inherited: parent
        );

        Assert.DoesNotContain("github_token", environment.Keys);
        Assert.Equal("survives", environment["Monkey"]);
    }

    [Fact]
    public void An_explicit_listed_credential_shaped_name_is_admitted()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GITHUB_TOKEN"] = "ghp_mine",
            ["NPM_TOKEN"] = "npm_ambient",
        };

        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            passthrough: ["GITHUB_TOKEN"],
            inherited: parent
        );

        // The pass-through is the opt-in: one exact name, and only that name.
        Assert.Equal("ghp_mine", environment["GITHUB_TOKEN"]);
        Assert.DoesNotContain("NPM_TOKEN", environment.Keys);
    }

    [Fact]
    public void A_declared_value_cannot_put_a_withheld_shaped_name_back()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            set: new Dictionary<string, string> { ["GITHUB_TOKEN"] = "re-added" },
            inherited: new Dictionary<string, string>()
        );

        // The shape rule is a deny for the whole child, so a spawn site cannot re-introduce a
        // withheld name by declaring it as an extra value.
        Assert.DoesNotContain("GITHUB_TOKEN", environment.Keys);
    }

    [Fact]
    public void The_scrubbed_policy_leaves_the_shape_rule_to_its_declared_names()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Scrubbed,
            Read(Parent),
            passthrough: ["GITHUB_TOKEN"]
        );

        // An extension host never receives the inherited set, so a credential-shaped name arrives
        // only when the host's own declared names ask for it.
        Assert.Equal("ghp_parent", environment["GITHUB_TOKEN"]);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", environment.Keys);
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
    public void The_inherited_policy_re_admits_an_exact_passed_through_name_after_the_strip()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            passthrough: ["ANTHROPIC_API_KEY"],
            strip: ["ANTHROPIC_API_KEY", "GEMINI_API_KEY"],
            inherited: Parent
        );

        // The explicit name wins over the deny for that exact variable only.
        Assert.Equal("sk-secret", environment["ANTHROPIC_API_KEY"]);
        Assert.DoesNotContain("GEMINI_API_KEY", environment.Keys);
        Assert.Equal("/usr/bin", environment["PATH"]);
    }

    [Fact]
    public void A_passed_through_name_the_parent_lacks_stays_absent()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            passthrough: ["MY_PROJECT_TOKEN"],
            inherited: Parent
        );

        // Never set to an empty value: a missing variable must not look like an empty one.
        Assert.DoesNotContain("MY_PROJECT_TOKEN", environment.Keys);
    }

    [Fact]
    public void The_pass_through_is_an_exact_name_not_a_pattern()
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["ANTHROPIC_API_KEY"] = "sk-secret",
            ["ANTHROPIC_API_KEY_BACKUP"] = "user-owned",
        };

        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            passthrough: ["ANTHROPIC_API_KEY_BACKUP", "ANTHROPIC_*"],
            strip: ["ANTHROPIC_API_KEY"],
            inherited: parent
        );

        // The look-alike the user listed arrives; the credential arrives only if named exactly.
        Assert.Equal("user-owned", environment["ANTHROPIC_API_KEY_BACKUP"]);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", environment.Keys);
    }

    [Fact]
    public void A_declared_value_cannot_put_a_passed_through_stripped_name_back()
    {
        IReadOnlyDictionary<string, string> environment = EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            set: new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "re-added" },
            passthrough: ["ANTHROPIC_API_KEY"],
            strip: ["ANTHROPIC_API_KEY"],
            inherited: Parent
        );

        // The pass-through re-admits the inherited value only; the strip stays a deny for values.
        Assert.Equal("sk-secret", environment["ANTHROPIC_API_KEY"]);
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
