namespace Lunate.Coding.Tests;

/// <summary>
/// The tool-process pass-through list: the exact names the user admits, applied after the
/// credential strip, and the one startup warning for a name that is also a credential. The list is
/// process-wide by design, so every test restores the empty default.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class ToolEnvironmentPassthroughTests
{
    [Fact]
    public void The_list_is_empty_until_it_is_configured()
    {
        ToolEnvironmentPassthrough.ResetForTests();

        Assert.Empty(ToolEnvironmentPassthrough.Names);
    }

    [Fact]
    public void Configure_keeps_the_exact_names_in_order_and_drops_duplicates()
    {
        try
        {
            IReadOnlyList<string> collisions = ToolEnvironmentPassthrough.Configure([
                " MY_PROJECT_TOKEN ",
                "PROXY_URL",
                "MY_PROJECT_TOKEN",
                "  ",
            ]);

            Assert.Empty(collisions);
            Assert.Equal(["MY_PROJECT_TOKEN", "PROXY_URL"], ToolEnvironmentPassthrough.Names);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    [Fact]
    public void A_listed_credential_name_is_reported_for_the_startup_warning()
    {
        try
        {
            IReadOnlyList<string> collisions = ToolEnvironmentPassthrough.Configure([
                "MY_PROJECT_TOKEN",
                "OPENAI_API_KEY",
            ]);

            Assert.Equal(["OPENAI_API_KEY"], collisions);
            // The explicit name is still passed through; the warning is the only signal.
            Assert.Equal(["MY_PROJECT_TOKEN", "OPENAI_API_KEY"], ToolEnvironmentPassthrough.Names);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    [Fact]
    public void A_name_that_only_looks_like_a_credential_does_not_collide()
    {
        try
        {
            Assert.Empty(
                ToolEnvironmentPassthrough.Configure(["OPENAI_API_KEY_BACKUP", "OPENAI_*"])
            );
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    [Fact]
    public void The_collision_warning_names_the_variables_and_the_reason()
    {
        string warning = ToolEnvironmentPassthrough.CollisionWarning([
            "OPENAI_API_KEY",
            "GEMINI_API_KEY",
        ]);

        Assert.Contains("toolEnvironment.passthrough", warning, StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", warning, StringComparison.Ordinal);
        Assert.Contains("GEMINI_API_KEY", warning, StringComparison.Ordinal);
        Assert.Contains("provider credential", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_clears_a_configured_list()
    {
        ToolEnvironmentPassthrough.Configure(["MY_PROJECT_TOKEN"]);

        ToolEnvironmentPassthrough.ResetForTests();

        Assert.Empty(ToolEnvironmentPassthrough.Names);
    }
}
