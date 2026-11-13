"""The `--self-test` pass: one small tree per failure path the checker must catch."""

import json
import os
import sys
import tempfile
from pathlib import Path

from .checks import check
from .run_examples import run_examples
from .text import write_text


def self_test():
    failures = []

    def expect(name, files, fails, contains=None):
        with tempfile.TemporaryDirectory(prefix="docs-check-self-test.", dir=os.environ.get("TMPDIR") or None) as tmp:
            root = Path(tmp)
            for relative, text in files.items():
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                write_text(path, text)
            problems = check(root)
            joined = "\n".join(problems)
            if fails and not problems:
                failures.append(f"{name}: expected a failure, got none")
            elif not fails and problems:
                failures.append(f"{name}: expected success, got:\n{joined}")
            elif fails and contains and contains not in joined:
                failures.append(f"{name}: expected '{contains}' in:\n{joined}")

    def manifest(navigation, redirects=None, prose_only=None):
        return json.dumps(
            {
                "navigation": navigation,
                "redirects": redirects or [],
                "proseOnly": prose_only or [],
            },
            indent=2,
        )

    def base_navigation(index=False):
        items = [
            {"title": "Start", "path": "start.md"},
            {"title": "Next", "path": "next.md"},
        ]
        if index:
            items.append({"title": "Home", "path": "index.md"})
        return [{"title": "Get started", "items": items}]

    def base(index=False):
        files = {
            "docs/docs.json": manifest(base_navigation(index)),
            "docs/start.md": "# Start\n\nSee [next](next.md#details).\n\n"
            "<!-- snippet: src/example.txt -->\n",
            "docs/next.md": "# Next\n\n## Details\n\n"
            "<!-- snippet: src/example.txt -->\n",
            "docs/README.md": "# Documentation map\n\nMaintainer page.\n",
            "src/example.txt": "example\n",
            "src/Lunate.Coding/settings-schema.json": json.dumps(
                {"version": 1, "entries": [{"name": "schemaVersion"}]}
            ),
        }
        if index:
            files["docs/index.md"] = (
                "# Home\n\n<!-- docs-sync: navigation -->\n"
                "- **Get started**\n  - [Start](start.md)\n  - [Next](next.md)\n"
                "  - [Home](index.md)\n<!-- /docs-sync: navigation -->\n\n"
                "<!-- snippet: src/example.txt -->\n"
            )
        return files

    def changed(files, **updates):
        result = dict(files)
        result.update(updates)
        return result

    expect("valid tree", base(), False)
    expect("valid tree with navigation region", base(index=True), False)
    expect(
        "missing navigation path",
        changed(base(), **{"docs/docs.json": manifest([{"title": "G", "items": [{"title": "Start", "path": "missing.md"}, {"title": "Next", "path": "next.md"}]}])}),
        True,
        "navigation path does not exist",
    )
    expect(
        "unlisted user page",
        changed(base(), **{"docs/extra.md": "# Extra\n\n<!-- snippet: src/example.txt -->\n"}),
        True,
        "user-path page is not in navigation",
    )
    expect(
        "duplicate navigation path",
        changed(base(), **{"docs/docs.json": manifest([{"title": "G", "items": [{"title": "Start", "path": "start.md"}, {"title": "Again", "path": "start.md"}, {"title": "Next", "path": "next.md"}]}])}),
        True,
        "appears more than once",
    )
    expect(
        "broken relative link",
        changed(base(), **{"docs/start.md": "# Start\n\n[gone](gone.md)\n\n<!-- snippet: src/example.txt -->\n"}),
        True,
        "missing link target",
    )
    expect(
        "broken anchor",
        changed(base(), **{"docs/start.md": "# Start\n\n[next](next.md#absent)\n\n<!-- snippet: src/example.txt -->\n"}),
        True,
        "missing anchor",
    )
    expect(
        "heading inside a fenced block is not an anchor",
        changed(
            base(),
            **{
                "docs/start.md": "# Start\n\n[next](next.md#ghost-anchor)\n\n"
                "<!-- snippet: src/example.txt -->\n",
                "docs/next.md": '# Next\n\n```bash\necho "\n## ghost-anchor\n"\n```\n\n'
                "<!-- snippet: src/example.txt -->\n",
            },
        ),
        True,
        "missing anchor",
    )
    expect(
        "missing redirect stub",
        changed(base(), **{"docs/docs.json": manifest(base_navigation(), [{"from": "old.md", "to": "next.md"}])}),
        True,
        "has no redirect stub",
    )
    expect(
        "maintainer rename without a stub",
        changed(
            base(),
            **{
                "docs/docs.json": manifest(
                    base_navigation(), [{"from": "old.md", "to": "dev/architecture.md"}]
                ),
                "docs/dev/architecture.md": "# Architecture\n\nMaintainer page.\n",
            },
        ),
        False,
    )
    redirect_stub = (
        "<!-- moved: next.md -->\n# Moved\n\n"
        "This page is now [Next](next.md). Update your bookmarks.\n"
    )
    expect(
        "valid redirect stub",
        changed(
            base(),
            **{
                "docs/docs.json": manifest(base_navigation(), [{"from": "old.md", "to": "next.md"}]),
                "docs/old.md": redirect_stub,
            },
        ),
        False,
    )
    expect(
        "redirect stub with extra content",
        changed(
            base(),
            **{
                "docs/docs.json": manifest(base_navigation(), [{"from": "old.md", "to": "next.md"}]),
                "docs/old.md": redirect_stub + "\nExtra paragraph.\n",
            },
        ),
        True,
        "only the marker",
    )
    expect(
        "prose-only without a reason",
        changed(
            base(),
            **{
                "docs/docs.json": manifest(base_navigation(), prose_only=[{"path": "start.md"}]),
                "docs/start.md": "# Start\n\nNo runnable example yet.\n",
            },
        ),
        True,
        "needs a reason",
    )
    expect(
        "unmarked user page",
        changed(base(), **{"docs/start.md": "# Start\n\nNo runnable example yet.\n"}),
        True,
        "has no snippet, config or run marker",
    )
    expect(
        "invalid config example",
        changed(
            base(),
            **{
                "docs/docs.json": manifest(
                    [{"title": "G", "items": [{"title": "Start", "path": "start.md"}, {"title": "Next", "path": "next.md"}]}]
                ),
                "docs/start.md": "# Start\n\n<!-- config: settings -->\n```json\n{\n```\n",
            },
        ),
        True,
        "not valid JSON",
    )
    expect(
        "settings key the store refuses",
        changed(
            base(),
            **{
                "docs/start.md": "# Start\n\n<!-- config: settings -->\n```json\n"
                '{"schemaVersion": 2, "notASetting": true}\n```\n'
            },
        ),
        True,
        "store refuses",
    )
    expect(
        "run marker without a reason",
        changed(
            base(),
            **{
                "docs/start.md": "# Start\n\n<!-- run: no -->\n```bash\nlunate --help\n```\n"
            },
        ),
        True,
        "needs a one-line reason",
    )
    expect(
        "navigation region mismatch",
        changed(
            base(index=True),
            **{
                "docs/index.md": "# Home\n\n<!-- docs-sync: navigation -->\n"
                "- **Wrong**\n<!-- /docs-sync: navigation -->\n"
            },
        ),
        True,
        "does not match docs/docs.json",
    )

    with tempfile.TemporaryDirectory(prefix="docs-check-self-test.", dir=os.environ.get("TMPDIR") or None) as tmp:
        root = Path(tmp)
        for relative, text in base().items():
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            write_text(path, text)
        binary = root / "fake-lunate"
        write_text(binary, "#!/bin/sh\nexit 0\n")
        binary.chmod(0o755)
        write_text(
            root / "docs/start.md",
            "# Start\n\n<!-- run: help -->\n```bash\nlunate --help\n```\n",
        )
        if run_examples(root, binary):
            failures.append("run: a succeeding help block must pass")
        write_text(binary, "#!/bin/sh\nexit 3\n")
        if not any("exited 3" in problem for problem in run_examples(root, binary)):
            failures.append("run: a failing help block must be reported")

    def cli_case(name, page, help_text, fails, contains):
        with tempfile.TemporaryDirectory(
            prefix="docs-check-self-test.", dir=os.environ.get("TMPDIR") or None
        ) as tmp:
            root = Path(tmp)
            files = base()
            files["docs/docs.json"] = manifest(
                base_navigation() + [{"title": "CLI", "path": "cli.md"}]
            )
            files["docs/cli.md"] = page
            for relative, text in files.items():
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                write_text(path, text)
            binary = root / "fake-lunate"
            write_text(binary, f"#!/bin/sh\nprintf '%s\\n' {help_text!r}\nexit 0\n")
            binary.chmod(0o755)
            problems = run_examples(root, binary)
            joined = "\n".join(problems)
            if fails and contains not in joined:
                failures.append(f"cli.md: {name}: expected '{contains}' in:\n{joined}")
            elif not fails and problems:
                failures.append(f"cli.md: {name}: expected success, got:\n{joined}")

    help_run = "<!-- run: help -->\n```bash\nlunate --help\n```\n"
    cli_page = "# CLI\n\nRun `lunate --help` or pass `--json`.\n\n" + help_run
    cli_case("matching options", cli_page, "lunate --help [--json]", False, "")
    cli_case(
        "documented option the help omits",
        cli_page,
        "lunate --help",
        True,
        "documents '--json', which the --help output does not list",
    )
    cli_case(
        "help option the page omits",
        "# CLI\n\n" + help_run,
        "lunate --help [--json]",
        True,
        "lists '--json', which the page does not document",
    )

    if failures:
        for failure in failures:
            print(f"docs-check: self-test: {failure}", file=sys.stderr)
        return 1
    print("docs-check: self-test passed")
    return 0
