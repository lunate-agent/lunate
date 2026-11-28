"""Pure helpers for the pinned SWE-bench run: definition, subset, spend and summary.

The orchestrator is ``scripts/eval/swebench_run.py``. These helpers do no subprocess work, so the
tests exercise the selection, cost and summary rules without Docker or a live model.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

SINGLE_MODEL_TIERS = ("budget", "frontier")
DECISION_TIER = "decision"
TIERS = (*SINGLE_MODEL_TIERS, DECISION_TIER)
DECISION_LAYERS = ("on", "off")


def load_definition(path: Path) -> dict:
    definition = json.loads(path.read_text(encoding="utf-8"))
    models = definition.get("models") or []
    single = [model for model in models if model.get("tier") in SINGLE_MODEL_TIERS]
    decisions = [model for model in models if model.get("tier") == DECISION_TIER]
    unknown = [model.get("tier") for model in models if model.get("tier") not in TIERS]
    if unknown:
        raise ValueError("run.json names an unknown arm tier: " + ", ".join(map(str, unknown)))
    if sorted(model.get("tier") for model in single) != sorted(SINGLE_MODEL_TIERS):
        raise ValueError("run.json must name exactly one budget model and one frontier model")
    if len(decisions) > 1:
        raise ValueError("run.json may name at most one decision arm")
    for model in decisions:
        validate_decision_arm(model)
    if not definition.get("dataset") or not definition.get("split"):
        raise ValueError("run.json must record the dataset name and split")
    limits = definition.get("limits") or {}
    if not isinstance(limits.get("maxCostUsd"), (int, float)) or limits["maxCostUsd"] <= 0:
        raise ValueError("run.json must record a positive maxCostUsd")
    if not definition.get("instancesFile"):
        raise ValueError("run.json must name the instancesFile")
    return definition


def validate_decision_arm(model: dict) -> None:
    """The third Lunate arm names two models, a layer value and why it exists."""
    for role in ("main", "decision"):
        nested = model.get(role)
        if not isinstance(nested, dict) or not nested.get("id"):
            raise ValueError(f"the decision arm must name a '{role}' model id")
        if not nested.get("credential"):
            raise ValueError(f"the decision arm's '{role}' model must name its credential variable")
    if model.get("decisionLayer") not in DECISION_LAYERS:
        raise ValueError("the decision arm's 'decisionLayer' must be 'on' or 'off'")
    if not model.get("note"):
        raise ValueError("the decision arm must carry a note saying what the layer does")


def decision_model_id(model: dict) -> str | None:
    """The decision model an arm runs, or None for a single-model arm."""
    nested = model.get("decision")
    return nested.get("id") if isinstance(nested, dict) else None


def arm_chat_model(model: dict) -> str:
    """The id the adapter passes as ``--model`` for either arm shape."""
    nested = model.get("main")
    return nested["id"] if isinstance(nested, dict) else model["id"]


def arm_credentials(model: dict) -> list[str]:
    """The credential variables an arm forwards to the container, by name, without duplicates."""
    names: list[str] = []
    for role in ("main", "decision"):
        nested = model.get(role)
        name = nested.get("credential") if isinstance(nested, dict) else None
        if name and name not in names:
            names.append(name)
    credential = model.get("credential")
    if credential and credential not in names:
        names.append(credential)
    return names


def provider_preset(model_id: str) -> str:
    """The preset a catalog id names: its first path segment (``opencode-go/glm-5.3-flash``)."""
    return model_id.split("/", 1)[0]


def decision_home_files(model: dict) -> dict[str, dict]:
    """The prepared home that turns the arm's definition into an in-image configuration.

    The home is the arm as data: one profile per model, and — when the arm says the layer is on —
    the ``decisions`` block with the decision profile and routing in shadow, so the decision model
    advises and the main model stays the chat model.
    """
    main = arm_chat_model(model)
    decision = decision_model_id(model)
    if decision is None:
        raise ValueError("only a decision arm carries a prepared home")
    settings: dict = {
        "schemaVersion": 2,
        "profiles": {
            "main": {"provider": provider_preset(main), "model": main},
            "decision": {"provider": provider_preset(decision), "model": decision},
        },
        "usages": {"chat": "main"},
    }
    if model.get("decisionLayer") == "on":
        settings["decisions"] = {
            "profile": "decision",
            "routing": "shadow",
            "routingProfiles": ["main", "decision"],
        }
    return {"settings.json": settings}


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


def trajectory_totals(path: Path, decision_model: str | None = None) -> dict:
    """The token, step, wall and price totals one trajectory carries.

    ``steps`` counts the chat model's calls only: a usage record that names the arm's decision model
    is a ``decisionSteps`` call, so a decision arm's step count stays comparable with a single-model
    arm's. ``uncostedRecords`` counts usage lines the run stamped no price on, which makes the
    instance unpriced for a multi-model arm instead of pricing it at one model's rate.
    """
    totals = {
        "steps": 0,
        "decisionSteps": 0,
        "usageRecords": 0,
        "uncostedRecords": 0,
        "inputTokens": 0,
        "outputTokens": 0,
        "cacheReadTokens": 0,
        "cacheWriteTokens": 0,
        "costUsd": 0.0,
        "recordCostUsd": 0.0,
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
            totals["usageRecords"] += 1
            if decision_model and event.get("modelId") == decision_model:
                totals["decisionSteps"] += 1
            else:
                totals["steps"] += 1
            usage = event.get("usage") or {}
            totals["inputTokens"] += int(usage.get("inputTokenCount") or 0)
            totals["outputTokens"] += int(usage.get("outputTokenCount") or 0)
            totals["cacheReadTokens"] += int(event.get("cacheReadTokens") or 0)
            totals["cacheWriteTokens"] += int(event.get("cacheWriteTokens") or 0)
            cost = event.get("costUsd")
            if isinstance(cost, (int, float)) and not isinstance(cost, bool):
                totals["costUsd"] += float(cost)
                totals["recordCostUsd"] += float(cost)
            else:
                totals["uncostedRecords"] += 1
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


def record_spend(totals: dict) -> float | None:
    """The sum of the per-record prices a multi-model trajectory carries, or None when unpriced."""
    if totals.get("uncostedRecords"):
        return None
    return round(totals.get("recordCostUsd") or 0.0, 9)


def arm_instance_spend(model: dict, totals: dict, pricing: dict | None) -> float | None:
    """The instance's spend for either arm shape: the cap event first, then prices.

    A decision arm runs two models, so its spend is the sum of the per-record prices the run
    stamped; a single-model arm keeps the lane price of its one model.
    """
    limit = totals.get("limit") or {}
    spend = limit.get("spendUsd")
    if limit.get("kind") == "cost" and isinstance(spend, (int, float)):
        return float(spend)
    if decision_model_id(model) is not None:
        return record_spend(totals)
    return priced_spend(pricing, totals)


def elapsed_seconds(started_at: object, finished_at: object) -> float | None:
    """The wall clock between two UTC stamps the run records, or None when either is unreadable."""
    if not isinstance(started_at, str) or not isinstance(finished_at, str):
        return None
    try:
        started = datetime.strptime(started_at, "%Y-%m-%dT%H:%M:%SZ")
        finished = datetime.strptime(finished_at, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        return None
    seconds = (finished.replace(tzinfo=timezone.utc) - started.replace(tzinfo=timezone.utc)).total_seconds()
    return round(seconds, 3) if seconds >= 0 else None


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
    resolved = sum(1 for row in rows if row.get("resolved"))
    spend = round(sum(spend_values), 6) if len(priced_rows) == len(rows) else None
    seconds = [row.get("seconds") for row in rows]
    timed = all(isinstance(value, (int, float)) and not isinstance(value, bool) for value in seconds)
    return {
        "arm": arm,
        "tier": tier,
        "model": model,
        "instances": rows,
        "resolved": resolved,
        "total": len(rows),
        "spendUsd": spend,
        "steps": sum(int(row.get("steps") or 0) for row in rows),
        "decisionSteps": sum(int(row.get("decisionSteps") or 0) for row in rows),
        "seconds": round(sum(seconds), 3) if rows and timed else None,
        # A per-resolve cost needs both a price and a resolve; anything else is unknown, not zero.
        "costPerResolvedUsd": (
            round(spend / resolved, 6) if spend is not None and resolved else None
        ),
        "unpricedInstances": [row["instanceId"] for row in rows if row.get("spendUsd") is None],
        "failures": [
            {"instanceId": row["instanceId"], "reason": row.get("failure")}
            for row in rows
            if row.get("failure")
        ],
    }


def format_seconds(seconds: object) -> str:
    return f"{seconds:.1f}s" if isinstance(seconds, (int, float)) else "unknown"


def format_usd(amount: object, digits: int = 4) -> str:
    return f"${amount:.{digits}f}" if isinstance(amount, (int, float)) else "unknown"


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
        "| Arm | Model | Tier | Resolved | Spend | Steps | Wall time | $/resolved | Failures |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for arm in summary["arms"]:
        spend = "unpriced" if arm["spendUsd"] is None else f"${arm['spendUsd']:.4f}"
        lines.append(
            f"| {arm['arm']} | `{arm['model']}` | {arm['tier']} | "
            f"{arm['resolved']}/{arm['total']} | {spend} | {arm.get('steps', 'unknown')} | "
            f"{format_seconds(arm.get('seconds'))} | "
            f"{format_usd(arm.get('costPerResolvedUsd'))} | {len(arm['failures'])} |"
        )
    if summary.get("capReached"):
        lines += ["", "The run reached its cost cap; later instances were not started."]
    lines.append("")
    return "\n".join(lines)
