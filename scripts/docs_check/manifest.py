"""Validating navigation nodes, redirect stubs and the generated navigation region."""

import re

from .constants import MOVED_MARKER
from .paths import docs_relative, normalize_docs_path
from .text import read_text


def flatten_navigation(nodes, problems, where="navigation"):
    entries = []
    if not isinstance(nodes, list):
        problems.append(f"{where}: must be a list")
        return entries
    for index, node in enumerate(nodes):
        label = f"{where}[{index}]"
        if not isinstance(node, dict):
            problems.append(f"{label}: must be an object")
            continue
        title = node.get("title")
        if not isinstance(title, str) or not title.strip():
            problems.append(f"{label}: has no title")
        if "path" in node and "items" in node:
            problems.append(f"{label}: has both a path and items")
            continue
        if "path" in node:
            path = normalize_docs_path(str(node["path"]))
            if path is None:
                problems.append(f"{label}: path must stay under docs/: {node['path']}")
            else:
                entries.append((title, path))
        elif "items" in node:
            entries.extend(flatten_navigation(node["items"], problems, f"{label}.items"))
        else:
            problems.append(f"{label}: has neither a path nor items")
    return entries


def check_stub(root, source, target):
    problems = []
    relative = docs_relative(root, source)
    lines = read_text(source).splitlines()
    while lines and not lines[-1].strip():
        lines.pop()
    if len(lines) != 4:
        return [f"{relative}: a redirect stub must carry only the marker and three lines"]
    marker = MOVED_MARKER.match(lines[0].strip())
    if not marker:
        return [f"{relative}: the first line must be '<!-- moved: <target> -->'"]
    if lines[1].strip() != "# Moved":
        problems.append(f"{relative}: the stub heading must be '# Moved'")
    if lines[2].strip():
        problems.append(f"{relative}: the stub needs a blank line after '# Moved'")
    sentence = re.match(
        r"^This page is now \[[^\]]+\]\(([^)]+)\)\. Update your bookmarks\.$", lines[3].strip()
    )
    if not sentence:
        return problems + [f"{relative}: the stub must be the standard moved-page sentence"]
    expected = (root / "docs" / target).resolve()
    marker_target = marker.group(1).strip()
    marker_paths = [
        (source.parent / marker_target).resolve(),
        (root / "docs" / marker_target).resolve(),
    ]
    if expected not in marker_paths:
        problems.append(f"{relative}: the moved marker does not point at {target}")
    link_target = sentence.group(1).strip()
    link_path = (source.parent / link_target).resolve()
    if link_path != expected:
        problems.append(f"{relative}: the moved-page link does not point at {target}")
    return problems


def check_navigation_region(root, navigation, problems):
    index = root / "docs" / "index.md"
    if not index.is_file():
        return
    start = "<!-- docs-sync: navigation -->"
    end = "<!-- /docs-sync: navigation -->"
    lines = read_text(index).splitlines()
    starts = [i for i, line in enumerate(lines) if line.strip() == start]
    ends = [i for i, line in enumerate(lines) if line.strip() == end]
    if len(starts) != 1 or len(ends) != 1:
        problems.append(
            f"docs/index.md: expected one '{start}' and one '{end}', found "
            f"{len(starts)} and {len(ends)}"
        )
        return
    if starts[0] > ends[0]:
        problems.append("docs/index.md: the navigation end marker comes before its start")
        return

    def render(nodes, depth=0):
        rendered = []
        for node in nodes:
            indent = "  " * depth
            if "items" in node:
                rendered.append(f"{indent}- **{node['title']}**")
                rendered.extend(render(node["items"], depth + 1))
            else:
                rendered.append(f"{indent}- [{node['title']}]({node['path']})")
        return rendered

    expected = render(navigation)
    actual = [line for line in lines[starts[0] + 1 : ends[0]] if line.strip()]
    if actual != expected:
        problems.append(
            "docs/index.md: the docs-sync: navigation region does not match docs/docs.json; "
            "expected:\n" + "\n".join(expected)
        )
