"""The `--check` pass: manifest, navigation, redirects, markers and links."""

import json
from pathlib import Path

from .constants import MANIFEST
from .links import check_links
from .manifest import check_navigation_region, check_stub, flatten_navigation
from .markers import scan_markers
from .paths import (
    docs_files,
    docs_relative,
    document_paths,
    is_maintainer,
    is_record,
    normalize_docs_path,
    relative_document,
)
from .text import posix, read_text


def check(root, manifest=None):
    root = Path(root).resolve()
    manifest_path = Path(manifest).resolve() if manifest else root / MANIFEST
    problems = []
    try:
        data = json.loads(read_text(manifest_path))
    except FileNotFoundError:
        return [f"{posix(manifest_path)}: manifest does not exist"]
    except json.JSONDecodeError as error:
        return [f"{posix(manifest_path)}: invalid JSON: {error.msg}"]
    if not isinstance(data, dict):
        return [f"{posix(manifest_path)}: the manifest must be a JSON object"]

    navigation = data.get("navigation")
    redirects = data.get("redirects", [])
    prose_only = data.get("proseOnly", [])
    if not isinstance(navigation, list):
        problems.append("docs/docs.json: navigation must be a list")
        navigation = []
    if not isinstance(redirects, list):
        problems.append("docs/docs.json: redirects must be a list")
        redirects = []
    if not isinstance(prose_only, list):
        problems.append("docs/docs.json: proseOnly must be a list")
        prose_only = []

    entries = flatten_navigation(navigation, problems)
    navigation_paths = []
    for title, path in entries:
        if not isinstance(title, str) or not title.strip():
            continue
        destination = root / "docs" / path
        if not destination.is_file():
            problems.append(f"docs/docs.json: navigation path does not exist: {path}")
        if path in navigation_paths:
            problems.append(f"docs/docs.json: navigation path appears more than once: {path}")
        navigation_paths.append(path)

    redirect_from = set()
    for index, entry in enumerate(redirects):
        label = f"docs/docs.json: redirects[{index}]"
        if not isinstance(entry, dict):
            problems.append(f"{label} must be an object")
            continue
        source = normalize_docs_path(str(entry.get("from", "")))
        target = normalize_docs_path(str(entry.get("to", "")))
        if source is None or target is None:
            problems.append(f"{label}: from and to must stay under docs/")
            continue
        if source in redirect_from:
            problems.append(f"{label}: from appears more than once: {source}")
        redirect_from.add(source)
        target_file = root / "docs" / target
        if not target_file.is_file():
            problems.append(f"{label}: target does not exist: {target}")
        source_file = root / "docs" / source
        if source_file.is_file():
            problems.extend(check_stub(root, source_file, target))
        elif not target.startswith("dev/"):
            problems.append(f"{label}: {source} has no redirect stub")

    user_pages = []
    for path in docs_files(root):
        relative = docs_relative(root, path)
        if is_record(posix(path.relative_to(root))) or is_maintainer(relative):
            continue
        if relative in redirect_from:
            continue
        user_pages.append(relative)

    user_set = set(user_pages)
    seen = set()
    for path in navigation_paths:
        if path in seen:
            continue
        seen.add(path)
        if path not in user_set:
            problems.append(
                f"docs/docs.json: navigation path is not a user-path page: {path}"
            )
    for page in user_pages:
        if page not in seen:
            problems.append(f"docs/docs.json: user-path page is not in navigation: {page}")

    prose_paths = {}
    for index, entry in enumerate(prose_only):
        label = f"docs/docs.json: proseOnly[{index}]"
        if not isinstance(entry, dict):
            problems.append(f"{label} must be an object")
            continue
        path = normalize_docs_path(str(entry.get("path", "")))
        reason = entry.get("reason")
        if path is None:
            problems.append(f"{label}: path must stay under docs/")
            continue
        if path not in navigation_paths:
            problems.append(f"{label}: path is not in navigation: {path}")
        if path in prose_paths:
            problems.append(f"{label}: path appears more than once: {path}")
        if not isinstance(reason, str) or not reason.strip():
            problems.append(f"{label}: needs a reason")
        prose_paths[path] = reason

    documents = document_paths(root)
    marker_pages = set()
    for path in documents:
        relative = relative_document(root, path)
        has_marker, _ = scan_markers(root, relative, read_text(path), problems)
        if has_marker and relative.startswith("docs/"):
            marker_pages.add(relative[len("docs/") :])
    for page in user_pages:
        if page not in marker_pages and page not in prose_paths:
            problems.append(
                f"docs/{page}: has no snippet, config or run marker and is not proseOnly"
            )

    check_links(root, documents, problems)
    check_navigation_region(root, navigation, problems)
    return problems
