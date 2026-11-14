"""Comparing the options `cli.md` documents with the `--help` output it runs.

The reference page names every option the product accepts; `lunate --help` is the surface of record.
In a `--run` pass the page's `<!-- run: help -->` blocks are executed, and every long option in the
page is compared with the union of those outputs in both directions: an option the page documents but
the help does not list is a page the product cannot honour, and an option the help lists but the page
omits is a reference that is no longer complete.
"""

import re

from .text import read_text

CLI_PAGE = "docs/cli.md"
OPTION = re.compile(r"(?<![\w-])(--[a-z][a-z0-9-]*)(?![\w-])")


def options_in(text):
    return {match.group(1) for match in OPTION.finditer(text)}


def documented_options(text):
    """Returns each documented option with the first line that names it."""
    found = {}
    for number, line in enumerate(text.splitlines(), start=1):
        for option in OPTION.findall(line):
            found.setdefault(option, number)
    return found


def check_cli_options(root, help_outputs, problems):
    """Compares the page's options with the help output of its own `run: help` blocks."""
    page = root / CLI_PAGE
    outputs = help_outputs.get(CLI_PAGE)
    if not page.is_file() or not outputs:
        return
    documented = documented_options(read_text(page))
    advertised = set()
    for output in outputs:
        advertised.update(options_in(output))
    for option, number in sorted(documented.items()):
        if option not in advertised:
            problems.append(
                f"{CLI_PAGE}:{number}: documents '{option}', which the --help output does not list"
            )
    for option in sorted(advertised - set(documented)):
        problems.append(
            f"{CLI_PAGE}: the --help output lists '{option}', which the page does not document"
        )
