"""Reading, fence stripping and Markdown anchor/link extraction."""

import re

from .constants import (
    COMMENT,
    FENCE_OPEN,
    HEADING,
    INLINE_CODE,
    LINK,
    REFERENCE_LINK,
)


def read_text(path):
    with open(path, "r", encoding="utf-8", newline="") as file:
        return file.read()


def write_text(path, text):
    with open(path, "w", encoding="utf-8", newline="") as file:
        file.write(text)


def posix(path):
    return path.as_posix()


def fenced_blocks(lines):
    """Returns (opening-line index, info string, body) for each fenced code block."""
    blocks = []
    index = 0
    while index < len(lines):
        match = FENCE_OPEN.match(lines[index])
        if not match:
            index += 1
            continue
        fence = match.group(1)
        char = fence[0]
        length = len(fence)
        info = match.group(2).strip()
        opening = index
        body = []
        index += 1
        close = re.compile(
            r"^[ \t]*" + re.escape(char) + "{" + str(length) + r",}[ \t]*$"
        )
        while index < len(lines) and not close.match(lines[index]):
            body.append(lines[index])
            index += 1
        blocks.append((opening, info, "\n".join(body)))
        index += 1
    return blocks


def previous_marker_line(lines, opening):
    index = opening - 1
    while index >= 0 and not lines[index].strip():
        index -= 1
    return index


def parse_run_marker(value):
    match = re.match(r"^(offline|help|no)\b(.*)$", value.strip())
    if not match:
        return None, ""
    kind = match.group(1)
    reason = match.group(2).strip()
    reason = re.sub(r"^[:\-–—]\s*", "", reason).strip()
    return kind, reason


def slugify(text):
    text = re.sub(r"!\[[^\]]*\]\([^)]*\)", "", text)
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)
    text = text.replace("`", "")
    text = re.sub(r"<[^>]+>", "", text)
    text = text.strip().lower()
    text = re.sub(r"[^\w\s-]", "", text)
    text = re.sub(r"\s+", "-", text)
    return text


def anchors_of(text):
    anchors = set()
    counts = {}
    for line in without_fences(text).splitlines():
        match = HEADING.match(line)
        if match:
            base = slugify(match.group(2))
            count = counts.get(base, 0)
            anchors.add(base if count == 0 else f"{base}-{count}")
            counts[base] = count + 1
        for name in re.findall(r'\b(?:id|name)="([^"]+)"', line):
            anchors.add(name)
    return anchors


def without_fences(text):
    lines = text.splitlines()
    kept = []
    fence = None
    for line in lines:
        opening = FENCE_OPEN.match(line)
        if fence is None and opening:
            fence = opening.group(1)
            kept.append("")
            continue
        if fence is not None:
            close = re.compile(
                r"^[ \t]*" + re.escape(fence[0]) + "{" + str(len(fence)) + r",}[ \t]*$"
            )
            if close.match(line):
                fence = None
            kept.append("")
            continue
        kept.append(line)
    return "\n".join(kept)


def links_of(text):
    text = without_fences(text)
    text = COMMENT.sub("", text)
    text = INLINE_CODE.sub("", text)
    links = [match.group(1).strip("<>") for match in LINK.finditer(text)]
    links.extend(match.group(1).strip("<>") for match in REFERENCE_LINK.finditer(text))
    return links
