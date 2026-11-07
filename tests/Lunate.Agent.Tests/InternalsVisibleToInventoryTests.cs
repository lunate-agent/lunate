using System.Globalization;
using System.Xml.Linq;
using Lunate.TestSupport;

namespace Lunate.Agent.Tests;

/// <summary>
/// The <c>InternalsVisibleTo</c> inventory: every <c>src/*/**.csproj</c> grants each friend
/// assembly at most once. Duplicate entries are noise the reader has to diff away (finding N-2);
/// the SDK already merges them, so a clean inventory changes no behaviour — this test only keeps
/// the list from growing a second copy again.
/// </summary>
public sealed class InternalsVisibleToInventoryTests
{
    [Fact]
    public void Every_friend_assembly_is_listed_once_per_project()
    {
        var duplicates = new List<string>();

        string sourceRoot = Path.Combine(RepositoryRoot.Find(), "src");
        foreach (
            string projectFile in Directory.EnumerateFiles(
                sourceRoot,
                "*.csproj",
                SearchOption.AllDirectories
            )
        )
        {
            foreach (
                var group in XDocument
                    .Load(projectFile)
                    .Descendants("InternalsVisibleTo")
                    .Select(entry => entry.Attribute("Include")?.Value)
                    .Where(include => !string.IsNullOrWhiteSpace(include))
                    .GroupBy(include => include!, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
            )
            {
                duplicates.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} grants '{1}' {2} times",
                        projectFile,
                        group.Key,
                        group.Count()
                    )
                );
            }
        }

        Assert.Empty(duplicates);
    }
}
