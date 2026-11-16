using System.Runtime.Versioning;
using Lunate.TestSupport;

namespace Lunate.Coding.Tests;

/// <summary>
/// <see cref="BuildOutput.TargetFramework"/> is what every test that spawns a sibling project's
/// binary names the framework folder by, so it must be the framework this build really targets —
/// a mismatch sends those tests looking in a folder the build no longer writes.
/// </summary>
public sealed class BuildOutputTests
{
    [Fact]
    public void The_derived_framework_is_the_one_this_assembly_targets()
    {
        var attribute = typeof(BuildOutputTests).Assembly.GetCustomAttributes(
            typeof(TargetFrameworkAttribute),
            inherit: false
        );

        var target = Assert.IsType<TargetFrameworkAttribute>(Assert.Single(attribute));

        // ".NETCoreApp,Version=v11.0" -> "net11.0": the folder name the build writes.
        Assert.Equal(".NETCoreApp", target.FrameworkName.Split(',')[0]);
        string version = target.FrameworkName.Split("Version=v")[^1];
        Assert.Equal(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"net{version}"
            ),
            BuildOutput.TargetFramework
        );
    }
}
