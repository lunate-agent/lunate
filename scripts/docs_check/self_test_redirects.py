"""The `--self-test` cases for redirect stubs and the records rule.

`docs/docs.json` may rename a maintainer page without a stub, and a link in `CHANGELOG.md` to a
renamed path is history rather than a dead link; everything else needs its four-line stub.
"""


def redirect_cases(expect, base, changed, manifest, base_navigation):
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
        "a changelog link to a documented maintainer move is history",
        changed(
            base(),
            **{
                "docs/docs.json": manifest(
                    base_navigation(), [{"from": "gone.md", "to": "dev/architecture.md"}]
                ),
                "docs/dev/architecture.md": "# Architecture\n\nMaintainer page.\n",
                "CHANGELOG.md": "# Changelog\n\nThe runbook is [here](docs/gone.md).\n",
            },
        ),
        False,
    )
    expect(
        "a changelog link no redirect records is still a dead link",
        changed(
            base(),
            **{"CHANGELOG.md": "# Changelog\n\nThe runbook is [here](docs/gone.md).\n"},
        ),
        True,
        "missing link target",
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
