using System.Reflection;

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
    public void The_collection_never_runs_beside_another_collection()
    {
        // A session composed outside this collection configures the same process-wide list, so
        // sequential tests inside the collection are not enough: the whole collection must run
        // alone. This pins the definition that keeps that outside Configure(empty) from racing a
        // read or a spawn here.
        CollectionDefinitionAttribute definition = Assert.IsType<CollectionDefinitionAttribute>(
            typeof(ToolEnvironmentCollection).GetCustomAttribute<CollectionDefinitionAttribute>()
        );
        Assert.True(
            definition.DisableParallelization,
            "the tool-environment collection must disable parallelization"
        );
    }

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
            ToolEnvironmentPassthrough.Admission admission = ToolEnvironmentPassthrough.Configure([
                " MY_PROJECT_TOKEN ",
                "PROXY_URL",
                "MY_PROJECT_TOKEN",
                "  ",
            ]);

            Assert.Empty(admission.Collisions);
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
            ToolEnvironmentPassthrough.Admission admission = ToolEnvironmentPassthrough.Configure([
                "MY_PROJECT_TOKEN",
                "OPENAI_API_KEY",
            ]);

            Assert.Equal(["OPENAI_API_KEY"], admission.Collisions);
            Assert.Equal(["MY_PROJECT_TOKEN", "OPENAI_API_KEY"], admission.Names);
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
                ToolEnvironmentPassthrough
                    .Configure(["OPENAI_API_KEY_BACKUP", "OPENAI_*"])
                    .Collisions
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
    public void The_admission_notice_names_the_variables_in_order()
    {
        string notice = ToolEnvironmentPassthrough.AdmissionNotice([
            "MY_PROJECT_TOKEN",
            "PROXY_URL",
        ]);

        Assert.Contains("toolEnvironment.passthrough", notice, StringComparison.Ordinal);
        Assert.Contains("MY_PROJECT_TOKEN, PROXY_URL", notice, StringComparison.Ordinal);
        Assert.Contains("tool processes", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void The_admission_notice_is_empty_for_an_empty_list() =>
        Assert.Equal(string.Empty, ToolEnvironmentPassthrough.AdmissionNotice([]));

    [Fact]
    public void Reset_clears_a_configured_list()
    {
        ToolEnvironmentPassthrough.Configure(["MY_PROJECT_TOKEN"]);

        ToolEnvironmentPassthrough.ResetForTests();

        Assert.Empty(ToolEnvironmentPassthrough.Names);
    }
}
