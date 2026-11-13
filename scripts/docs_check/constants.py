"""Paths, sets and regular expressions shared by the checker modules."""

import re
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path("docs") / "docs.json"
ROOT_DOCUMENTS = ("README.md", "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md", "AGENTS.md")
RECORD_PREFIXES = (
    "docs/dev/spikes/",
    "docs/dev/eval/",
)
MAINTAINER_FILES = {"README.md", "architecture.md", "distribution.md", "guide.md"}
MAINTAINER_PREFIXES = ("media/", "dev/")
SETTINGS_SCHEMA = Path("src/Lunate.Coding/settings-schema.json")
CONFIG_KINDS = ("settings", "mcp", "providers", "extension", "auth")

FENCE_OPEN = re.compile(r"^[ \t]{0,3}(`{3,}|~{3,})([^\n]*)$")
LINK = re.compile(
    r"!?\[[^\]]*\]\(\s*(<[^>\n]*>|[^)\s]+)(?:\s+(?:\"[^\"]*\"|'[^']*'|\([^)]*\)))?\s*\)"
)
REFERENCE_LINK = re.compile(r"^[ \t]{0,3}\[[^\]]+\]:\s*(\S+)", re.M)
COMMENT = re.compile(r"<!--.*?-->", re.S)
INLINE_CODE = re.compile(r"`[^`\n]*`")
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*#*\s*$")
CONFIG_MARKER = re.compile(r"^<!--\s*config:\s*(.*?)\s*-->$")
RUN_MARKER = re.compile(r"^<!--\s*run:\s*(.*?)\s*-->$")
SNIPPET_MARKER = re.compile(r"^<!--\s*snippet:\s*(.*?)\s*-->$")
MOVED_MARKER = re.compile(r"^<!--\s*moved:\s*(.*?)\s*-->$")
