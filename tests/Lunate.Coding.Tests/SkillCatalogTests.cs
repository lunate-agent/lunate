namespace Lunate.Coding.Tests;

/// <summary>The catalog's index wiring: overflow marks entries not-indexed for /skills.</summary>
public sealed class SkillCatalogTests
{
    [Fact]
    public void BuildIndex_marks_overflowed_skills_not_indexed()
    {
        using var temp = new TempDirectory();
        for (var index = 0; index < 60; index++)
        {
            string directory = Path.Combine(temp.Root, ".agents", "skills", $"skill-{index:D2}");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "SKILL.md"),
                $"---\nname: skill-{index:D2}\ndescription: {new string('d', 200)}\n---\nBody\n"
            );
        }

        SkillCatalog catalog = SkillCatalog.Discover(
            new SkillScanRequest(temp.Root, null, temp.File("home"), [])
        );
        catalog.SetProjectTrusted(true);
        catalog.BuildIndex(temp.Root, 2000, null);

        Assert.NotEmpty(catalog.Index.NotIndexed);
        foreach (string name in catalog.Index.NotIndexed)
        {
            SkillDescriptor skill = Assert.Single(catalog.Skills, entry => entry.Name == name);
            Assert.True(skill.NotIndexed);
        }

        string view = SkillsView.Text(catalog.Skills);
        Assert.Contains("(project, not indexed)", view, StringComparison.Ordinal);
    }

    [Fact]
    public void The_repositorys_project_skills_are_all_indexed_with_their_descriptions()
    {
        string repository = Lunate.TestSupport.RepositoryRoot.Find();
        SkillCatalog catalog = SkillCatalog.Discover(
            new SkillScanRequest(
                repository,
                repository,
                Path.Combine(Path.GetTempPath(), "lunate-repro-631"),
                []
            )
        );
        catalog.SetProjectTrusted(true);
        catalog.BuildIndex(repository, 2000, null);

        int valid = catalog
            .Skills.Count(skill =>
                skill is { Valid: true, CommandOnly: false, Untrusted: false }
            );
        Assert.Equal(valid, catalog.Index.IndexedCount);
        Assert.Empty(catalog.Index.NotIndexed);
    }
}
