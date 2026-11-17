"""Pins the template roundtrip's packages folder and its source mapping (#932, #936).

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
`HandleAsync`. Both restores now use a packages folder of their own, so what the gate and the test
prove is this checkout's shape and never a cache's.

The second half is the sources (#936). Restoring into a fresh folder removes the *installed* copy, but
it still leaves "which of two sources provides this id and version" to NuGet -- and a release
publishes the same contract on nuget.org under the same id and version. Naming the packed feed first
is an ordering rule, not a guarantee: the source that answers is the source that provides the package,
and on a runner whose NuGet cache is warm that can be the released copy. Run 44033633019's three
Windows legs -- `verify (windows-latest, 1/4)`, `(2/4)` and `verify-powershell (windows)`, the three
legs whose gate restores the template -- went red while every other leg of that run passed, so the
roundtrip no longer leaves the question to ordering at all: a NuGet configuration of the run's own
maps every `Lunate.*` package to the packed feed, and reads the machine's configuration not at all
(`--configfile`). nuget.org stays, for the test SDK the copy's test project needs.

Four things are pinned here: the wiring in `scripts/verify.sh`, the copy end-to-end test's restore in
`ExtensionTemplateCopyEndToEndTests.cs`, the same two properties in the `verify.ps1` twin, and -- with
a stand-in package pair and local folder feeds, no network, no build -- the restore behaviour the
wiring depends on. The stand-in pair is what makes the last part cheap: a feed serves marker
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
SDK_ID = "TestOnly.Sdk"
PACKAGE_VERSION = "1.0.0"


def without_comments(text: str) -> str:
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("#"))


def roundtrip() -> str:
    """The contract roundtrip step of verify.sh, without its comments."""
    text = VERIFY.read_text(encoding="utf-8")
    start = text.index(ROUNDTRIP_STEP)
    return without_comments(text[start : text.index(ROUNDTRIP_END, start)])


def powershell_roundtrip() -> str:
    """The contract roundtrip step of verify.ps1, without its comments."""
    text = without_comments(VERIFY_POWERSHELL.read_text(encoding="utf-8"))
    start = text.index("contract package roundtrip")
    return text[start : text.index('Write-Host "`n==> format"', start)]


def package(feed: Path, marker: str, package_id: str = PACKAGE_ID) -> Path:
    """A nupkg whose content is the marker, so a restore can be asked which copy it bound."""
    nuspec = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">\n'
        "  <metadata>\n"
        f"    <id>{package_id}</id>\n"
        f"    <version>{PACKAGE_VERSION}</version>\n"
        "    <authors>lunate-tests</authors>\n"
        "    <description>stand-in package</description>\n"
        "  </metadata>\n"
        "</package>\n"
    )
    path = feed / f"{package_id}.{PACKAGE_VERSION}.nupkg"
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        archive.writestr("marker.txt", marker)
    return path


def project(root: Path, packages: list[str] | None = None) -> Path:
    references = packages or [PACKAGE_ID]
    items = "".join(
        f'    <PackageReference Include="{package_id}" Version="{PACKAGE_VERSION}" />\n'
        for package_id in references
    )
    path = root / "Consumer.csproj"
    path.write_text(
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        "  <PropertyGroup>\n"
        "    <TargetFramework>net11.0</TargetFramework>\n"
        "  </PropertyGroup>\n"
        "  <ItemGroup>\n"
        f"{items}"
        "  </ItemGroup>\n"
        "</Project>\n",
        encoding="utf-8",
    )
    return path


def config(root: Path, sources: list[tuple[str, Path]], mapped: list[str]) -> Path:
    """A nuget.config whose mapping sends `mapped` patterns to the first source, and the rest to
    every source in the order given (a `<clear />`ed list, like the roundtrip's own file)."""
    entries = "".join(
        f'    <add key="{key}" value="{value}" />\n' for key, value in sources
    )
    mapping = "".join(
        f'    <packageSource key="{key}">\n'
        + "".join(f'      <package pattern="{pattern}" />\n' for pattern in patterns)
        + "    </packageSource>\n"
        for key, patterns in (("packed", mapped), ("nuget.org", ["*"]))
    )
    path = root / "nuget.config"
    path.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<configuration>\n"
        "  <packageSources>\n"
        "    <clear />\n"
        f"{entries}"
        "  </packageSources>\n"
        "  <packageSourceMapping>\n"
        f"{mapping}"
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
    return (packages / PACKAGE_ID.lower() / PACKAGE_VERSION / "marker.txt").read_text(
        encoding="utf-8"
    )


def restore_with_config(consumer: Path, nuget_config: Path, packages: Path) -> None:
    """One restore through a configuration file of its own, the way the roundtrip runs it."""
    shutil.rmtree(consumer.parent / "obj", ignore_errors=True)
    subprocess.run(
        [
            "dotnet",
            "restore",
            str(consumer),
            "--configfile",
            str(nuget_config),
            "--packages",
            str(packages),
            "--nologo",
            "-nodeReuse:false",
        ],
        check=True,
        capture_output=True,
        text=True,
    )


def marker(packages: Path) -> str:
    return (packages / PACKAGE_ID.lower() / PACKAGE_VERSION / "marker.txt").read_text(
        encoding="utf-8"
    )


class RoundtripWiringTest(unittest.TestCase):
    """What verify.sh hands the template's restores and builds."""

    def test_each_restore_owns_a_packages_folder_and_a_configuration_file(self):
        block = roundtrip()

        self.assertIn(
            'template_packages="$(native_path "${contract_tmp}/packages")"',
            block,
            "the roundtrip must restore into a packages folder of its own, named once",
        )
        self.assertIn(
            'contract_config="${contract_tmp}/nuget.config"',
            block,
            "the roundtrip must name the NuGet configuration it restores through, once",
        )
        self.assertEqual(
            2,
            block.count('--packages "$template_packages"'),
            "both template restores must use that packages folder",
        )
        self.assertEqual(
            2,
            block.count('--configfile "$contract_config"'),
            "both template restores must read the roundtrip's own configuration",
        )
        self.assertEqual(2, block.count("dotnet restore"), "one restore per template project")
        self.assertNotIn(
            "--source",
            block,
            "the source list is the configuration file's, not a flag: a source that answers is not "
            "a source that was named first",
        )
        self.assertNotIn(
            "RestoreAdditionalProjectSources",
            block,
            "the packed feed is the roundtrip's source, not an addition to the machine's sources",
        )

    def test_the_configuration_maps_every_lunate_package_to_the_packed_feed(self):
        block = roundtrip()

        self.assertIn("<clear />", block, "the machine's sources must not be inherited")
        self.assertIn(
            '<add key="packed" value="${contract_feed}" />',
            block,
            "the packed feed is the first source the configuration names",
        )
        self.assertIn(
            f'<add key="nuget.org" value="{NUGET_ORG}" />',
            block,
            "nuget.org stays for the test SDK the template's test project needs",
        )
        self.assertIn(
            '<package pattern="Lunate.*" />',
            block,
            "a Lunate package may come from the packed feed, whatever the source order is",
        )
        self.assertIn('<package pattern="*" />', block, "every other package stays reachable")

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

    def test_the_copy_end_to_end_test_restores_through_the_same_configuration(self):
        # The other half of the same failure: the copy end-to-end test in Lunate.Extensibility.Testing.Tests
        # also packs the contract packages and builds a copy of the template with NuGet references. On
        # `verify (macos-latest, 2/2)` it bound the published 1.16.0 contract from the runner's packages
        # folder -- where CS8509 is only a warning outside Directory.Build.props -- and failed later at
        # load with a TypeLoadException on HandleAsync. One helper serves both projects, so it must
        # restore each with a private packages folder, its own configuration and the packed feed.
        text = TEMPLATE_COPY_TEST.read_text(encoding="utf-8")
        start = text.index("private static void RestoreAndBuild(")
        block = text[start : text.index("private static void Dotnet(", start)]

        self.assertIn('restore "{project}"', block, "every copy build starts from a restore")
        self.assertIn(
            '--configfile "{config}"',
            block,
            "the restore reads the configuration the packed feed is mapped in",
        )
        self.assertIn(
            '--packages "{packages}"',
            block,
            "the restore must not read the machine's packages folder",
        )
        self.assertNotIn(
            "--source",
            block,
            "the sources are the configuration's: naming the feed first is an ordering rule, not "
            "a guarantee",
        )
        self.assertIn(
            "--no-restore", block, "the build must use the assets file that restore wrote"
        )
        self.assertNotIn(
            "RestoreAdditionalProjectSources",
            block,
            "the packed feed is the source, not an addition to whatever the machine has",
        )
        # The configuration the helper writes, asserted on the whole file: it lives beside the
        # restore helpers, not inside them.
        self.assertIn("<clear />", text, "the machine's sources must not be inherited")
        self.assertIn("<packageSourceMapping>", text, "the packed feed must be the mapped source")
        self.assertIn(
            '<package pattern="Lunate.*" />',
            text,
            "a Lunate package may come from the packed feed, whatever the source order is",
        )
        self.assertIn(
            '<add key="nuget.org" value="{NuGetOrg}" />',
            text,
            "nuget.org still serves the test SDK the copy's test project needs",
        )

    def test_the_powershell_twin_keeps_the_same_two_properties(self):
        # The Windows gate is the bash gate's twin (parity is what verify.ps1 is for), so the
        # packages folder and the mapped feed have to be there too, or the same red returns on the
        # PowerShell leg whose runner cache holds a released contract.
        block = powershell_roundtrip()

        self.assertEqual(2, block.count('--packages "$templatePackages"'))
        self.assertEqual(2, block.count('--configfile "$contractConfig"'))
        self.assertEqual(2, block.count("--no-restore"), "a build must not re-restore in verify.ps1")
        self.assertNotIn("--source", block)
        self.assertNotIn("RestoreAdditionalProjectSources", block)
        self.assertIn("<clear />", block)
        self.assertIn('<package pattern="Lunate.*" />', block)


class RestoreBindingTest(unittest.TestCase):
    """The restore behaviour the roundtrip's packages folder exists for."""

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


class SourceMappingTest(unittest.TestCase):
    """What the roundtrip's configuration decides when two sources serve the same version."""

    def setUp(self):
        if shutil.which("dotnet") is None:
            self.fail("the dotnet SDK is required: this test restores a real package")

    def test_the_mapping_wins_over_the_source_order(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            published = root_path / "published"
            published.mkdir()
            package(published, "published")
            packed = root_path / "packed"
            packed.mkdir()
            package(packed, "packed")

            # The released feed is named *first* on purpose: an ordering rule would bind the
            # published copy, which is the red #936 triages. The mapping must decide instead.
            nuget_config = config(
                root_path,
                [("nuget.org", published), ("packed", packed)],
                mapped=["Lunate.*"],
            )
            packages = root_path / "packages"
            consumer = project(root_path)
            restore_with_config(consumer, nuget_config, packages)

            self.assertEqual(
                "packed",
                marker(packages),
                "a mapped Lunate package must come from the packed feed, whatever the source order "
                "or a warm runner cache would answer",
            )

    def test_the_mapping_still_lets_the_test_sdk_come_from_the_released_feed(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            published = root_path / "published"
            published.mkdir()
            package(published, "published")
            package(published, "sdk", package_id=SDK_ID)
            packed = root_path / "packed"
            packed.mkdir()
            package(packed, "packed")

            nuget_config = config(
                root_path,
                [("packed", packed), ("nuget.org", published)],
                mapped=["Lunate.*"],
            )
            packages = root_path / "packages"
            consumer = project(root_path, packages=[PACKAGE_ID, SDK_ID])
            restore_with_config(consumer, nuget_config, packages)

            self.assertEqual("packed", marker(packages), "the contract stays the packed one")
            self.assertTrue(
                (packages / SDK_ID.lower() / PACKAGE_VERSION).is_dir(),
                "the pattern `*` must keep a package the packed feed cannot serve reachable",
            )


if __name__ == "__main__":
    unittest.main()
