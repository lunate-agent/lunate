"""Pure helpers for the pinned SWE-bench run: definition, subset, spend and summary.

The orchestrator is ``scripts/eval/swebench_run.py``. These helpers do no subprocess work, so the
tests exercise the selection, cost and summary rules without Docker or a live model.
"""

from __future__ import annotations

import json
from pathlib import Path

TIERS = ("budget", "frontier")


def load_definition(path: Path) -> dict:
    definition = json.loads(path.read_text(encoding="utf-8"))
    models = definition.get("models") or []
    tiers = [model.get("tier") for model in models]
    if sorted(tiers) != sorted(TIERS):
        raise ValueError("run.json must name exactly one budget model and one frontier model")
    if not definition.get("dataset") or not definition.get("split"):
        raise ValueError("run.json must record the dataset name and split")
    limits = definition.get("limits") or {}
    if not isinstance(limits.get("maxCostUsd"), (int, float)) or limits["maxCostUsd"] <= 0:
        raise ValueError("run.json must record a positive maxCostUsd")
    if not definition.get("instancesFile"):
        raise ValueError("run.json must name the instancesFile")
    return definition


def load_subset(path: Path) -> list[str]:
    ids = [
        line.strip()
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.strip().startswith("#")
    ]
    if not ids:
        raise ValueError(f"{path} names no instances")
    duplicates = sorted({instance for instance in ids if ids.count(instance) > 1})
    if duplicates:
        raise ValueError("duplicate instances: " + ", ".join(duplicates))
    return ids


def model_pricing(catalog_path: Path, model_id: str) -> dict | None:
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    for entry in catalog.get("models", []):
        if entry.get("id") == model_id:
            return entry.get("pricing")
    return None


def trajectory_totals(path: Path) -> dict:
    totals = {
        "steps": 0,
        "inputTokens": 0,
        "outputTokens": 0,
        "cacheReadTokens": 0,
        "cacheWriteTokens": 0,
        "costUsd": 0.0,
        "limit": None,
    }
    if not path.is_file():
        return totals
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line.strip():
            continue
        event = json.loads(line)
        kind = event.get("type")
        if kind == "usage_updated":
            totals["steps"] += 1
            usage = event.get("usage") or {}
            totals["inputTokens"] += int(usage.get("inputTokenCount") or 0)
            totals["outputTokens"] += int(usage.get("outputTokenCount") or 0)
            totals["cacheReadTokens"] += int(event.get("cacheReadTokens") or 0)
            totals["cacheWriteTokens"] += int(event.get("cacheWriteTokens") or 0)
            if isinstance(event.get("costUsd"), (int, float)):
                totals["costUsd"] += float(event["costUsd"])
        elif kind == "cost_limit_reached":
            totals["limit"] = {
                "kind": "cost",
                "maxCostUsd": event.get("maxCostUsd"),
                "spendUsd": event.get("spendUsd"),
            }
        elif kind == "time_limit_reached":
            totals["limit"] = {"kind": "time", "maxTimeSeconds": event.get("maxTimeSeconds")}
    return totals


def priced_spend(pricing: dict | None, totals: dict) -> float | None:
    """The catalog price for one instance's token lanes, or None when a used lane is unpriced."""
    if not pricing:
        return None
    cache_read = totals["cacheReadTokens"]
    cache_write = totals["cacheWriteTokens"]
    if cache_read > totals["inputTokens"] - cache_write:
        return None
    uncached = totals["inputTokens"] - cache_read - cache_write
    lanes = (
        (uncached, pricing.get("input")),
        (cache_read, pricing.get("cacheRead")),
        (cache_write, pricing.get("cacheWrite")),
        (totals["outputTokens"], pricing.get("output")),
    )
    cost = 0.0
    for tokens, rate in lanes:
        if tokens and not isinstance(rate, (int, float)):
            return None
        cost += tokens * float(rate or 0)
    return cost / 1_000_000


def instance_spend(totals: dict, pricing: dict | None) -> float | None:
    if totals.get("limit", {}) and totals["limit"].get("kind") == "cost":
        spend = totals["limit"].get("spendUsd")
        if isinstance(spend, (int, float)):
            return float(spend)
    return priced_spend(pricing, totals)


def official_counts(run_root: Path) -> dict | None:
    path = run_root / "official-results.json"
    if not path.is_file():
        return None
    results = json.loads(path.read_text(encoding="utf-8"))
    return {
        "resolved": int(results.get("resolved_instances") or 0),
        "unresolved": int(results.get("unresolved_instances") or 0),
        "total": int(results.get("dataset_instances") or 0),
        "resolvedIds": list(results.get("resolved_ids") or []),
        "unresolvedIds": list(results.get("unresolved_ids") or []),
    }


def summarize_arm(arm: str, tier: str, model: str, rows: list[dict]) -> dict:
    spend_values = [row["spendUsd"] for row in rows if row.get("spendUsd") is not None]
    priced_rows = [row for row in rows if row.get("spendUsd") is not None]
    return {
        "arm": arm,
        "tier": tier,
        "model": model,
        "instances": rows,
        "resolved": sum(1 for row in rows if row.get("resolved")),
        "total": len(rows),
        "spendUsd": round(sum(spend_values), 6) if len(priced_rows) == len(rows) else None,
        "unpricedInstances": [row["instanceId"] for row in rows if row.get("spendUsd") is None],
        "failures": [
            {"instanceId": row["instanceId"], "reason": row.get("failure")}
            for row in rows
            if row.get("failure")
        ],
    }


def render_markdown(summary: dict) -> str:
    total = (
        "unknown (an instance could not be priced)"
        if summary["totalSpendUsd"] is None
        else f"${summary['totalSpendUsd']:.4f}"
    )
    lines = [
        f"# SWE-bench first run — {summary['runId']}",
        "",
        f"Dataset: `{summary['dataset']}` (`{summary['split']}`), subset: {summary['total']} instances.",
        f"Cost cap: ${summary['maxCostUsd']:.2f}; reported spend: {total}.",
        "",
        "| Arm | Model | Tier | Resolved | Spend | Failures |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for arm in summary["arms"]:
        spend = "unpriced" if arm["spendUsd"] is None else f"${arm['spendUsd']:.4f}"
        lines.append(
            f"| {arm['arm']} | `{arm['model']}` | {arm['tier']} | "
            f"{arm['resolved']}/{arm['total']} | {spend} | {len(arm['failures'])} |"
        )
    if summary.get("capReached"):
        lines += ["", "The run reached its cost cap; later instances were not started."]
    lines.append("")
    return "\n".join(lines)
