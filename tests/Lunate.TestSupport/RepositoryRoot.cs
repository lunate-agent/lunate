using System.Globalization;

namespace Lunate.TestSupport;

/// <summary>
/// The one repository-root locator. Tests read committed fixtures relative to the directory that
/// holds <c>lunate.slnx</c>; this walks up to it so no test project carries its own copy of the walk,
/// its own marker spelling, or its own message.
/// </summary>
public static class RepositoryRoot
{
    /// <summary>The solution file that marks the repository root.</summary>
    public const string Marker = "lunate.slnx";

    /// <summary>The repository root above the running test binary.</summary>
    public static string Find() => Find(AppContext.BaseDirectory);

    /// <summary>
    /// Walks up from <paramref name="startDirectory"/> and returns the first directory holding
    /// <c>lunate.slnx</c>.
    /// </summary>
    /// <param name="startDirectory">The directory the walk starts at.</param>
    /// <returns>The full path of the repository root.</returns>
    /// <exception cref="InvalidOperationException">
    /// Neither the start directory nor any parent holds the marker.
    /// </exception>
    public static string Find(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        for (
            DirectoryInfo? directory = new(startDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, Marker)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Could not find {Marker} in '{startDirectory}' or any of its parents."
            )
        );
    }

    /// <summary>
    /// The one file named <paramref name="fileName" /> under a repository-relative directory such as
    /// <c>src/Lunate.Coding</c> or <c>tests/Lunate.Coding.Tests</c>. Feature folders group files and
    /// move them (#695), so a source scan asks for the file's name instead of pinning the folder it
    /// happened to sit in when the scan was written.
    /// </summary>
    /// <param name="directory">The repository-relative directory to search, with forward slashes.</param>
    /// <param name="fileName">The exact file name to look for.</param>
    /// <returns>The full path of the one match.</returns>
    /// <exception cref="InvalidOperationException">
    /// The directory holds no such file, or more than one, so a scan naming it would be ambiguous.
    /// </exception>
    public static string FindFile(string directory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string root = Find();
        string start = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
        string[] matches =
        [
            .. Directory
                .EnumerateFiles(start, fileName, SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Order(StringComparer.Ordinal),
        ];

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Expected one '{fileName}' under '{directory}', found {matches.Length}: {string.Join(", ", matches)}"
                )
            );
        }

        return matches[0];
    }

    private static bool IsBuildOutput(string file)
    {
        string[] segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.Ordinal)
            || segments.Contains("bin", StringComparer.Ordinal);
    }
}
