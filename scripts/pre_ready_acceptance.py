#!/usr/bin/env python3
"""The acceptance-check half of `scripts/pre-ready.py`: parse the linked issue, the answers and their
evidence. No IO, so `tests/scripts/test_pre_ready.py` can drive it with fixtures.
"""

from __future__ import annotations

import re

from pre_ready_checks import CheckResult

HEADING = re.compile(r"^(#{1,6})\s+(.*\S)\s*$")
LIST_ITEM = re.compile(r"^(\s*)(?:[-*+]|\d+[.)])\s+(.*\S)\s*$")
ANSWER_START = re.compile(
    r"^\s{0,3}(?:[-*+]\s+)?(?:acceptance\s+)?(?:check\s+)?(?:\*\*)?(\d{1,3})(?:\*\*)?"
    r"\s*(?:[.):\-\u2013\u2014]|\s)\s*",
    re.IGNORECASE,
)
TABLE_ANSWER = re.compile(r"^\s*\|\s*(\d{1,3})\s*\|")
NOT_MET = re.compile(r"\b(?:not|partially)\s+met\b", re.IGNORECASE)
MET = re.compile(r"\bmet\b", re.IGNORECASE)
FILE_LINE = re.compile(
    r"(?<![\w/.-])(?:[A-Za-z0-9_.-]+/)*[A-Za-z0-9_.-]+\.[A-Za-z0-9]+:\d+(?!\d)"
)
TEST_NAME = re.compile(
    r"\b(?:[A-Za-z_][A-Za-z0-9_]*\.)*"
    r"(?:[A-Za-z_][A-Za-z0-9_]*Tests?\.[A-Za-z_][A-Za-z0-9_]*|test_[A-Za-z0-9_]+)\b"
)
ISSUE_REFERENCE = re.compile(r"(?<![\w/])#(\d+)\b")
CLOSING_REFERENCE = re.compile(
    r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s*#(\d+)\b", re.IGNORECASE
)


def linked_issues(title: str, body: str) -> list[int]:
    """The issues a pull request links; a closing keyword wins over a passing reference."""
    text = f"{title}\n{body or ''}"
    closing = {int(match.group(1)) for match in CLOSING_REFERENCE.finditer(text)}
    if closing:
        return sorted(closing)
    return sorted({int(match.group(1)) for match in ISSUE_REFERENCE.finditer(text)})


def acceptance_items(body: str) -> list[str] | None:
    """The first-level list items under the first Acceptance heading, or None without one."""
    lines = (body or "").splitlines()
    level = 0
    start = 0
    for index, line in enumerate(lines):
        match = HEADING.match(line)
        if match and match.group(2).strip().lower().startswith("acceptance"):
            level = len(match.group(1))
            start = index + 1
            break
    else:
        return None

    items: list[str] = []
    base_indent: int | None = None
    for line in lines[start:]:
        heading = HEADING.match(line)
        if heading and len(heading.group(1)) <= level:
            break
        item = LIST_ITEM.match(line)
        if item is None:
            continue
        indent = len(item.group(1).expandtabs(4))
        if base_indent is None:
            base_indent = indent
        if indent == base_indent:
            items.append(item.group(2))
    return items


def answer_blocks(text: str) -> list[tuple[int, str]]:
    """Split a Markdown text into numbered blocks, joining wrapped continuation lines."""
    blocks: list[tuple[int, list[str]]] = []
    current: tuple[int, list[str]] | None = None
    for line in (text or "").splitlines():
        start = ANSWER_START.match(line) or TABLE_ANSWER.match(line)
        if start:
            if current is not None:
                blocks.append(current)
            current = (int(start.group(1)), [line])
            continue
        if current is None:
            continue
        if HEADING.match(line):
            blocks.append(current)
            current = None
            continue
        current[1].append(line)
    if current is not None:
        blocks.append(current)
    return [(number, "\n".join(lines)) for number, lines in blocks]


def collect_answers(texts: list[str]) -> dict[int, str]:
    """The last answer for each number across the body and the comments, in that order."""
    answers: dict[int, str] = {}
    for text in texts:
        for number, block in answer_blocks(text):
            if NOT_MET.search(block) or MET.search(block):
                answers[number] = block
    return answers


def answer_is_met(block: str) -> bool:
    return not NOT_MET.search(block) and bool(MET.search(block))


def answer_has_evidence(block: str) -> bool:
    return bool(FILE_LINE.search(block) or TEST_NAME.search(block))


def check_acceptance(
    title: str,
    body: str,
    comments: list[dict],
    issues: dict[int, dict],
) -> CheckResult:
    numbers = linked_issues(title, body)
    if not numbers:
        return CheckResult(
            False,
            "no issue is linked in the pull request title or body; name the issue this pull "
            "request closes with `Closes #<number>` so its acceptance checks can be read",
        )

    with_acceptance: list[tuple[int, list[str]]] = []
    missing: list[int] = []
    for number in numbers:
        issue = issues.get(number)
        if issue is None:
            missing.append(number)
            continue
        items = acceptance_items(issue.get("body") or "")
        if items:
            with_acceptance.append((number, items))
    if not with_acceptance:
        linked = ", ".join(f"#{number}" for number in numbers)
        unavailable = (
            f" (could not read {', '.join(f'#{number}' for number in missing)})" if missing else ""
        )
        return CheckResult(
            False,
            f"the linked issue(s) {linked}{unavailable} carry no Acceptance section with list "
            "items; link the issue this pull request closes with `Closes #<number>`",
        )

    answers = collect_answers([body or "", *[comment.get("body") or "" for comment in comments]])
    problems: list[str] = []
    for number, items in with_acceptance:
        for index in range(1, len(items) + 1):
            block = answers.get(index)
            if block is None:
                problems.append(
                    f"acceptance check {index} of #{number} is unanswered; answer it in the pull "
                    f"request body or final comment as `{index}. **met** — <file:line or test name>`"
                )
                continue
            if not answer_is_met(block):
                problems.append(
                    f"acceptance check {index} of #{number} is not met; finish it or park the card "
                    "— a pull request is not ready while an acceptance check is not met"
                )
                continue
            if not answer_has_evidence(block):
                problems.append(
                    f"acceptance check {index} of #{number} says met but carries no evidence; add a "
                    "`file:line` or a test name"
                )
    if problems:
        return CheckResult(False, " ".join(problems))
    total = sum(len(items) for _, items in with_acceptance)
    linked = " ".join(f"#{number} ({len(items)})" for number, items in with_acceptance)
    return CheckResult(True, f"{total} acceptance check(s) answered with evidence: {linked}")
