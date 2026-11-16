namespace Lunate.TestSupport;

/// <summary>
/// Where a test run's own build output lives. A test that spawns a sibling project's binary names
/// the folder by its target framework, and hard-coding that framework turns a target move into a
/// silent miss: the build writes <c>bin/Release/net11.0/</c> while the test looks in
/// <c>bin/Release/net10.0/</c>. Every such path asks here instead.
/// </summary>
public static class BuildOutput
{
    /// <summary>The target framework of this process's build, e.g. <c>net11.0</c>.</summary>
    /// <remarks>
    /// The test host runs from <c>&lt;project&gt;/bin/&lt;configuration&gt;/&lt;tfm&gt;/</c>, so the
    /// last segment of <see cref="AppContext.BaseDirectory"/> names the framework.
    /// </remarks>
    public static string TargetFramework { get; } = ReadTargetFramework();

    private static string ReadTargetFramework()
    {
        string directory = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(directory);
    }
}
