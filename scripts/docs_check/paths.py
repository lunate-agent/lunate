"""Walking the documentation tree and naming its pages."""

from pathlib import Path

from .constants import MAINTAINER_FILES, MAINTAINER_PREFIXES, RECORD_PREFIXES, ROOT_DOCUMENTS
from .text import posix


def is_record(relative):
    return any(relative == prefix.rstrip("/") or relative.startswith(prefix) for prefix in RECORD_PREFIXES)


def is_maintainer(relative):
    if relative in MAINTAINER_FILES:
        return True
    return any(relative.startswith(prefix) for prefix in MAINTAINER_PREFIXES)


def docs_files(root):
    docs = root / "docs"
    if not docs.is_dir():
        return []
    return sorted(path for path in docs.rglob("*.md") if path.is_file())


def document_paths(root):
    paths = []
    for name in ROOT_DOCUMENTS:
        path = root / name
        if path.is_file():
            paths.append(path)
    paths.extend(docs_files(root))
    return paths


def relative_document(root, path):
    return posix(path.relative_to(root))


def docs_relative(root, path):
    return posix(path.relative_to(root / "docs"))


def normalize_docs_path(value):
    if not value.strip():
        return None
    path = Path(value)
    if path.is_absolute() or ".." in path.parts or posix(path) == ".":
        return None
    return posix(path)
