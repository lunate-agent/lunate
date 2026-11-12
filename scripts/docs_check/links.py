"""Resolving every relative link and anchor the documentation carries."""

from .paths import is_record, relative_document
from .text import anchors_of, links_of, read_text


def resolve_link(root, source, target):
    path_part, _, fragment = target.partition("#")
    if not path_part:
        destination = source
    elif path_part.startswith("/"):
        destination = root / path_part.lstrip("/")
    else:
        destination = (source.parent / path_part).resolve()
    return destination, fragment


def check_links(root, paths, problems):
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
                problems.append(f"{relative}: missing link target: {target}")
                continue
            if fragment and destination.suffix.lower() == ".md":
                if destination not in anchor_cache:
                    anchor_cache[destination] = anchors_of(read_text(destination))
                if fragment not in anchor_cache[destination]:
                    problems.append(f"{relative}: missing anchor '{target}'")
