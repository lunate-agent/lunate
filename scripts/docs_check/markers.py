"""Validating the snippet, config and run markers above fenced blocks."""

import json

from .constants import (
    CONFIG_KINDS,
    CONFIG_MARKER,
    RUN_MARKER,
    SETTINGS_SCHEMA,
    SNIPPET_MARKER,
)
from .text import fenced_blocks, parse_run_marker, posix, previous_marker_line, read_text


def validate_config(root, kind, body, where, problems):
    if kind not in CONFIG_KINDS:
        problems.append(f"{where}: unknown config kind '{kind}'")
        return
    try:
        document = json.loads(body)
    except json.JSONDecodeError as error:
        problems.append(f"{where}: the {kind} example is not valid JSON: {error.msg}")
        return
    if kind != "settings":
        return
    if not isinstance(document, dict):
        problems.append(f"{where}: the settings example must be a JSON object")
        return
    schema_path = root / SETTINGS_SCHEMA
    if not schema_path.is_file():
        problems.append(f"{where}: cannot check settings keys without {posix(SETTINGS_SCHEMA)}")
        return
    schema = json.loads(read_text(schema_path))
    allowed = {entry["name"].split(".", 1)[0] for entry in schema.get("entries", [])}
    unknown = sorted(key for key in document if key not in allowed)
    if unknown:
        problems.append(
            f"{where}: settings example uses key(s) the store refuses: {', '.join(unknown)}"
        )


def scan_markers(root, relative, text, problems):
    """Returns (has-a-marker, runnable blocks) and reports broken markers."""
    lines = text.splitlines()
    used = set()
    runs = []
    has_marker = False

    for opening, info, body in fenced_blocks(lines):
        marker_index = previous_marker_line(lines, opening)
        marker = lines[marker_index].strip() if marker_index >= 0 else ""
        config_match = CONFIG_MARKER.match(marker)
        run_match = RUN_MARKER.match(marker)
        info_parts = info.split()
        kind = None
        if config_match:
            kind = config_match.group(1).strip()
            used.add(marker_index)
        if kind is not None:
            has_marker = True
            where = f"{relative}:{opening + 1}"
            if not info_parts or info_parts[0] not in ("json",):
                problems.append(f"{where}: a config marker must sit above a fenced JSON block")
            else:
                validate_config(root, kind, body, where, problems)

        if run_match:
            used.add(marker_index)
            has_marker = True
            where = f"{relative}:{opening + 1}"
            kind, reason = parse_run_marker(run_match.group(1))
            if kind is None:
                problems.append(
                    f"{where}: '{run_match.group(1).strip()}' is not offline, help or no"
                )
            else:
                if not info_parts or info_parts[0] not in ("bash", "sh"):
                    problems.append(f"{where}: a run marker must sit above a fenced bash block")
                if kind == "no" and not reason:
                    problems.append(f"{where}: 'run: no' needs a one-line reason")
                runs.append((kind, reason, relative, opening + 1, body))

    for index, line in enumerate(lines):
        stripped = line.strip()
        if (CONFIG_MARKER.match(stripped) or RUN_MARKER.match(stripped)) and index not in used:
            problems.append(
                f"{relative}:{index + 1}: marker '{stripped}' is not above a fenced block"
            )
        snippet = SNIPPET_MARKER.match(stripped)
        if snippet:
            has_marker = True
            target = root / snippet.group(1).strip()
            if not target.is_file():
                problems.append(
                    f"{relative}:{index + 1}: snippet source does not exist: "
                    f"{snippet.group(1).strip()}"
                )
    return has_marker, runs
