"""Resolving every relative link and anchor the documentation carries."""

from .paths import docs_relative, is_record, relative_document
from .text import anchors_of, links_of, read_text

CHANGELOG = "CHANGELOG.md"


def resolve_link(root, source, target):
    path_part, _, fragment = target.partition("#")
    if not path_part:
        destination = source
    elif path_part.startswith("/"):
        destination = root / path_part.lstrip("/")
    else:
        destination = (source.parent / path_part).resolve()
    return destination, fragment


def is_recorded_move(root, source, destination, redirects):
    """A changelog keeps the paths it was written with; a documented redirect covers the move.

    `CHANGELOG.md` is a record (`AGENTS.md`, "records are history"): an entry names the document
    that owned the fact when it was written, and only the release pull request may edit the file.
    A link that no longer resolves is therefore accepted when `docs/docs.json` records where the
    page went, and reported like any other dead link when it does not.
    """
    if relative_document(root, source) != CHANGELOG:
        return False
    try:
        return docs_relative(root, destination) in redirects
    except ValueError:
        return False


def check_links(root, paths, problems, redirects=frozenset()):
    anchor_cache = {}
    for source in paths:
        relative = relative_document(root, source)
        if is_record(relative):
            continue
        text = read_text(source)
        for target in links_of(text):
            if not target or target.startswith(("http://", "https://", "mailto:", "tel:", "#")):
                if target.startswith("#") and target[1:] not in anchors_of(text):
                    problems.append(f"{relative}: missing anchor '{target}'")
                continue
            if "://" in target or target.startswith("data:"):
                continue
            destination, fragment = resolve_link(root, source, target)
            try:
                destination.relative_to(root)
            except ValueError:
                problems.append(f"{relative}: link escapes the repository: {target}")
                continue
            if not destination.exists():
                if not is_recorded_move(root, source, destination, redirects):
                    problems.append(f"{relative}: missing link target: {target}")
                continue
            if fragment and destination.suffix.lower() == ".md":
                if destination not in anchor_cache:
                    anchor_cache[destination] = anchors_of(read_text(destination))
                if fragment not in anchor_cache[destination]:
                    problems.append(f"{relative}: missing anchor '{target}'")
