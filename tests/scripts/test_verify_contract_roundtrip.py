"""Pins the template roundtrip's packages folder and its package source mapping (#932, #937).

The verify gate packs the four SDK packages at the version the release carries and builds the
packaged-extension template against that feed, so the NuGet shape the gate proves is the one
nuget.org serves after the next tag. While a release is out, that version is *also* served by a feed,
which means a machine's global packages folder can already hold a copy of the same id and version
whose content is an earlier commit's -- and NuGet binds an installed copy before it asks any source.

Run 44012040507's two macOS legs did exactly that: `verify (macos-latest, 1/2)` built the template
against the published 1.16.0 contract instead of this checkout's, where the hook-result families had
just become unions, and failed on the switch the union closes (CS8509 in
`TemplateToolCallingHandler`); `verify (macos-latest, 2/2)` ran the copy end-to-end test, which packs
the same packages and builds a copy of the template outside `Directory.Build.props`, so the same
published contract compiled with only a warning and failed later at load with a TypeLoadException on
`HandleAsync`. Both restores use a packages folder of their own and name the feed they just packed,
so what the gate and the test prove is this checkout's shape and never a cache's.

Run 44033633019 showed that a packages folder alone is not enough on every runner: the three
Windows legs that restore the template (`verify (windows-latest, 1/4)` and `(2/4)`, and
`verify-powershell (windows)`) failed while every other leg passed, because "the packed feed is a
source and it is listed first" leaves *which* source provides an id and version to source order, the
HTTP cache and the machine's NuGet configuration -- and a released contract sits on nuget.org under
the same id and version. `scripts/verify.sh` now carries a `nuget.config` of its own with
`packageSourceMapping`, so a `Lunate.*` package may come from the packed feed and from nowhere else
(#937).

What is pinned here: the wiring in `scripts/verify.sh`, the copy end-to-end test's restore in
`ExtensionTemplateCopyEndToEndTests.cs`, the PowerShell twin's restore in `scripts/verify.ps1`, and --
with a stand-in package pair and two local folder feeds, no network, no build -- the restore behaviour
the wiring depends on. The stand-in pair is what makes the last part cheap: the feed serves marker
`packed`, a second feed serves `published` under the same id and version, and which marker lands in
the packages folder is the whole question.
"""

import shutil
import subprocess
import tempfile
import unittest
import zipfile
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
VERIFY = REPOSITORY_ROOT / "scripts" / "verify.sh"
VERIFY_POWERSHELL = REPOSITORY_ROOT / "scripts" / "verify.ps1"
TEMPLATE_COPY_TEST = (
    REPOSITORY_ROOT
    / "tests"
    / "Lunate.Extensibility.Testing.Tests"
    / "ExtensionTemplateCopyEndToEndTests.cs"
)
NUGET_ORG = "https://api.nuget.org/v3/index.json"

ROUNDTRIP_STEP = 'step "contract package roundtrip"'
ROUNDTRIP_END = "# The platform-independent half of the gate"

PACKAGE_ID = "Lunate.StandIn"
PACKAGE_VERSION = "1.0.0"


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def roundtrip() -> str:
    """The contract roundtrip step of verify.sh, without its comments."""
    text = VERIFY.read_text(encoding="utf-8")
    start = text.index(ROUNDTRIP_STEP)
    return without_comments(text[start : text.index(ROUNDTRIP_END, start)])


def package(feed: Path, marker: str) -> Path:
    """A nupkg whose content is the marker, so a restore can be asked which copy it bound."""
    nuspec = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">\n'
        "  <metadata>\n"
        f"    <id>{PACKAGE_ID}</id>\n"
        f"    <version>{PACKAGE_VERSION}</version>\n"
        "    <authors>lunate-tests</authors>\n"
        "    <description>stand-in package</description>\n"
        "  </metadata>\n"
        "</package>\n"
    )
    path = feed / f"{PACKAGE_ID}.{PACKAGE_VERSION}.nupkg"
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{PACKAGE_ID}.nuspec", nuspec)
        archive.writestr("marker.txt", marker)
    return path


def project(root: Path) -> Path:
    path = root / "Consumer.csproj"
    path.write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
        "  <PropertyGroup>\n"
        "    <TargetFramework>net11.0</TargetFramework>\n"
        "  </PropertyGroup>\n"
        "  <ItemGroup>\n"
        f'    <PackageReference Include="{PACKAGE_ID}" Version="{PACKAGE_VERSION}" />\n'
        "  </ItemGroup>\n"
        "</Project>\n",
        encoding="utf-8",
    )
    return path


def mapping_config(root: Path, packed: Path, other: Path) -> Path:
    """A configuration file shaped like the roundtrip's: Lunate.* from one feed, the rest elsewhere."""
    path = root / "nuget.config"
    path.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<configuration>\n"
        "  <packageSources>\n"
        "    <clear />\n"
        f'    <add key="packed" value="{packed}" />\n'
        f'    <add key="other" value="{other}" />\n'
        "  </packageSources>\n"
        "  <packageSourceMapping>\n"
        '    <packageSource key="packed">\n'
        '      <package pattern="Lunate.*" />\n'
        "    </packageSource>\n"
        '    <packageSource key="other">\n'
        '      <package pattern="*" />\n'
        "    </packageSource>\n"
        "  </packageSourceMapping>\n"
        "</configuration>\n",
        encoding="utf-8",
    )
    return path


def restore(consumer: Path, feed: Path, packages: Path) -> str:
    # A second restore of the same project would no-op against the assets file the first wrote, so
    # every restore here starts from scratch: the packages folder is the only state that carries over.
    shutil.rmtree(consumer.parent / "obj", ignore_errors=True)
    subprocess.run(
        [
            "dotnet",
            "restore",
            str(consumer),
            "--packages",
            str(packages),
            "--source",
            str(feed),
            "--nologo",
            "-nodeReuse:false",
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    return marker(packages)


def restore_with_config(consumer: Path, config: Path, packages: Path) -> str:
    """The same restore, with the roundtrip's own configuration file instead of source arguments."""
    shutil.rmtree(consumer.parent / "obj", ignore_errors=True)
    subprocess.run(
        [
            "dotnet",
            "restore",
            str(consumer),
            "--configfile",
            str(config),
            "--packages",
            str(packages),
            "--nologo",
            "-nodeReuse:false",
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    return marker(packages)


def marker(packages: Path) -> str:
    return (packages / PACKAGE_ID.lower() / PACKAGE_VERSION / "marker.txt").read_text(
        encoding="utf-8"
    )


class RoundtripWiringTest(unittest.TestCase):
    """What verify.sh hands the template's restores and builds."""

    def test_each_restore_owns_a_packages_folder_and_the_roundtrip_config(self):
        block = roundtrip()

        self.assertIn(
            'template_packages="$(native_path "${contract_tmp}/packages")"',
            block,
            "the roundtrip must restore into a packages folder of its own, named once",
        )
        self.assertIn(
            'contract_config="$(native_path "${contract_tmp}/nuget.config")"',
            block,
            "the roundtrip must restore with a configuration file of its own, named once",
        )
        self.assertEqual(
            2,
            block.count('--packages "$template_packages"'),
            "both template restores must use that packages folder",
        )
        self.assertEqual(
            2,
            block.count('--configfile "$contract_config"'),
            "both template restores must read that configuration file: machine sources and "
            "source order must not decide which feed provides the contract",
        )
        self.assertEqual(2, block.count("dotnet restore"), "one restore per template project")
        self.assertNotIn(
            "--source",
            block,
            "a source flag leaves the choice between the packed feed and nuget.org to ordering; "
            "packageSourceMapping is what makes the packed feed the only source for Lunate.*",
        )
        self.assertNotIn(
            "RestoreAdditionalProjectSources",
            block,
            "the packed feed is the roundtrip's source, not an addition to the machine's sources",
        )

    def test_the_roundtrip_config_maps_the_lunate_packages_to_the_packed_feed(self):
        block = roundtrip()

        self.assertIn("<clear />", block, "the machine's own sources must not be read")
        self.assertIn(
            '<add key="packed" value="${contract_feed}" />',
            block,
            "the feed the step just packed is a source of the roundtrip's configuration",
        )
        self.assertIn(
            f'<add key="nuget.org" value="{NUGET_ORG}" />',
            block,
            "nuget.org still serves the test SDK the template's test project needs",
        )
        self.assertIn(
            '<package pattern="Lunate.*" />',
            block,
            "a Lunate package may come from the packed feed and from nowhere else",
        )
        self.assertIn('<package pattern="*" />', block, "every other package comes from nuget.org")
        self.assertEqual(
            2,
            block.count('<packageSource key="'),
            "both source-to-package mappings belong to the roundtrip's configuration",
        )

    def test_each_build_uses_the_assets_file_the_restore_wrote(self):
        block = roundtrip()

        builds = [
            line
            for line in block.splitlines()
            if line.strip().startswith("dotnet build")
        ]
        self.assertEqual(2, len(builds), "one build per template project")
        # The flags continue on later lines, so the whole restored block is what says it.
        self.assertEqual(
            2,
            block.count("--no-restore"),
            "a build must not re-restore with the machine's sources and flip the assets file back",
        )


    def test_the_copy_end_to_end_test_restores_into_a_packages_folder_of_its_own(self):
        # The other half of the same failure: the copy end-to-end test in Lunate.Extensibility.Testing.Tests
        # also packs the contract packages and builds a copy of the template with NuGet references. On
        # `verify (macos-latest, 2/2)` it bound the published 1.16.0 contract from the runner's packages
        # folder -- where CS8509 is only a warning outside Directory.Build.props -- and failed later at
        # load with a TypeLoadException on HandleAsync. One helper serves both projects, so it must
        # restore and build each with a private packages folder and the packed feed.
        text = TEMPLATE_COPY_TEST.read_text(encoding="utf-8")
        start = text.index("private static void RestoreAndBuild(")
        block = text[start : text.index("private static void Dotnet(", start)]

        self.assertIn('restore "{project}"', block, "every copy build starts from a restore")
        self.assertIn(
            '--packages "{packages}"',
            block,
            "the restore must not read the machine's packages folder",
        )
        self.assertIn('--source "{feed}"', block, "the packed feed is the contract source")
        self.assertIn(
            '--source "{NuGetOrg}"',
            block,
            "nuget.org still serves the test SDK the copy's test project needs",
        )
        self.assertIn(
            f'private const string NuGetOrg = "{NUGET_ORG}";',
            text,
            "the fallback source is nuget.org itself",
        )
        self.assertIn(
            "--no-restore", block, "the build must use the assets file that restore wrote"
        )
        self.assertNotIn(
            "RestoreAdditionalProjectSources",
            block,
            "the packed feed is the source, not an addition to whatever the machine has",
        )

    def test_the_copy_end_to_end_test_pins_each_child_to_folders_of_its_own(self):
        # A shard runs its test assemblies beside each other, and the copy test now starts a pack,
        # two restores and two builds of its own. Restore children that share the temp directory, the
        # CLI home, the NuGet HTTP cache or the MSBuild server are what turned a loaded Windows
        # runner red before (scripts/Lunate.TestSupport/FixtureRestore.cs records the same finding),
        # so each child gets those folders pinned to this test and a single node.
        text = TEMPLATE_COPY_TEST.read_text(encoding="utf-8")
        helper = text[
            text.index("private static void Dotnet(") : text.index(
                "private static string Subdirectory("
            )
        ]
        for variable in (
            "DOTNET_CLI_HOME",
            "NUGET_HTTP_CACHE_PATH",
            "TMPDIR",
            "TMP",
            "TEMP",
            "MSBUILDUSESERVER",
            "DOTNET_CLI_USE_MSBUILD_SERVER",
        ):
            self.assertIn(
                f'start.EnvironmentVariables["{variable}"]',
                helper,
                f"every child dotnet must pin {variable} to the test's own folder",
            )

        children = text[text.index("private static void Pack(") : text.index("private static void Dotnet(")]
        self.assertEqual(
            3,
            children.count("-maxcpucount:1"),
            "the pack, the restore and the build each bound their own node",
        )

    def test_the_powershell_twin_keeps_the_same_two_properties(self):
        # The Windows gate is the bash gate's twin (parity is what verify.ps1 is for), so the
        # packages folder and the pinned feed have to be there too, or the same red returns on the
        # PowerShell leg whose runner cache holds a released contract.
        text = without_comments(VERIFY_POWERSHELL.read_text(encoding="utf-8"))
        start = text.index("contract package roundtrip")
        block = text[start : text.index('Write-Host "`n==> format"', start)]

        self.assertEqual(2, block.count('--packages "$templatePackages"'))
        self.assertEqual(2, block.count('--source "$contractFeed"'))
        self.assertEqual(2, block.count("--no-restore"), "a build must not re-restore in verify.ps1")
        self.assertNotIn("RestoreAdditionalProjectSources", block)


class RestoreBindingTest(unittest.TestCase):
    """The restore behaviour the roundtrip's packages folder and mapping exist for."""

    def setUp(self):
        if shutil.which("dotnet") is None:
            self.fail("the dotnet SDK is required: this test restores a real package")

    def test_a_fresh_packages_folder_binds_the_feed_it_is_told_to(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            feed = root_path / "feed"
            feed.mkdir()
            package(feed, "packed")

            self.assertEqual(
                "packed",
                restore(project(root_path), feed, root_path / "packages"),
                "a restore must bind the package the feed serves",
            )

    def test_package_source_mapping_binds_the_packed_feed_not_the_published_copy(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            published = root_path / "published"
            published.mkdir()
            package(published, "published")
            packed = root_path / "packed"
            packed.mkdir()
            package(packed, "packed")

            self.assertEqual(
                "packed",
                restore_with_config(
                    project(root_path),
                    mapping_config(root_path, packed, published),
                    root_path / "packages",
                ),
                "packageSourceMapping must make the packed feed the only source for Lunate.*, so "
                "source order, the HTTP cache and the machine's NuGet configuration cannot decide "
                "which shape the gate proved (the three Windows legs of run 44033633019)",
            )

    def test_an_installed_package_of_the_same_version_wins_over_the_feed(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            published = root_path / "published"
            published.mkdir()
            package(published, "published")
            packed = root_path / "packed"
            packed.mkdir()
            package(packed, "packed")
            packages = root_path / "packages"

            # What a machine's global packages folder holds after an earlier run made the same
            # id and version: the release's content, installed and therefore authoritative.
            self.assertEqual("published", restore(project(root_path), published, packages))

            self.assertEqual(
                "published",
                restore(project(root_path), packed, packages),
                "an installed copy of the version wins over the feed that serves it, which is the "
                "defect #932's packages folder removes: a roundtrip that restores into the machine's "
                "folder proves the release's shape, not this checkout's",
            )


if __name__ == "__main__":
    unittest.main()
