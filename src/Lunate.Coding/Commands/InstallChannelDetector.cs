namespace Lunate.Coding;

/// <summary>
/// Names the install channel from the executable path. The four channels have stable path markers:
/// a dotnet tool lives under <c>.dotnet/tools</c>, Homebrew under its <c>Cellar/lunate</c>, Scoop
/// under <c>scoop/apps/lunate</c>; anything else is the self-contained archive (including a
/// working-tree build).
/// </summary>
internal static class InstallChannelDetector
{
    internal const string DotnetTool = "dotnet tool";
    internal const string SelfContained = "self-contained";
    internal const string Homebrew = "Homebrew";
    internal const string Scoop = "Scoop";

    internal static string Detect(string? processPath, string? baseDirectory)
    {
        string path = (processPath ?? string.Empty) + "\n" + (baseDirectory ?? string.Empty);
        string normalized = path.Replace('\\', '/');
        if (normalized.Contains("/.dotnet/tools/", StringComparison.OrdinalIgnoreCase))
        {
            return DotnetTool;
        }

        if (normalized.Contains("/cellar/lunate/", StringComparison.OrdinalIgnoreCase))
        {
            return Homebrew;
        }

        if (normalized.Contains("/scoop/apps/lunate/", StringComparison.OrdinalIgnoreCase))
        {
            return Scoop;
        }

        return SelfContained;
    }
}
