"""The mini-swe-agent baseline arm of the pinned SWE-bench run."""

from __future__ import annotations

import json
import os
from pathlib import Path

from swebench_process import run_command, safe

REPOSITORY = Path(__file__).resolve().parents[2]
SWEBENCH_DIR = REPOSITORY / "eval" / "swebench"


def baseline_environment(definition: dict, model: dict) -> dict[str, str]:
    env = os.environ.copy()
    mapping = (definition.get("baseline", {}).get("env") or {}).get(model["id"], {})
    for target, source in mapping.items():
        value = os.environ.get(source)
        if value:
            env[target] = value
    env["MSWEA_COST_TRACKING"] = "default"
    return env


def baseline_command(definition: dict, instance_id: str, model: dict, trajectory: Path, remaining: float) -> list[str]:
    baseline = definition["baseline"]
    alias = baseline["models"][model["id"]]
    registry = (SWEBENCH_DIR / "litellm-models.json").resolve()
    cost_limit = min(float(baseline.get("costLimitUsd") or remaining), remaining)
    return [
        definition.get("_mini_extra") or "mini-extra",
        "swebench-single",
        "--subset",
        definition["dataset"],
        "--split",
        definition["split"],
        "--instance",
        instance_id,
        "--model",
        alias,
        "--yolo",
        "-c",
        "swebench.yaml",
        "-c",
        str((SWEBENCH_DIR / "mini-swe-agent.yaml").resolve()),
        "-c",
        f"agent.step_limit={baseline.get('stepLimit', 50)}",
        "-c",
        f"agent.cost_limit={cost_limit:.4f}",
        "-c",
        f"model.litellm_model_registry={registry}",
        "-o",
        str(trajectory),
    ]


def baseline_counts(work_root: Path, run_id: str) -> dict | None:
    summaries = sorted(work_root.glob(f"*.{run_id}.json"), key=lambda path: path.stat().st_mtime)
    if not summaries:
        return None
    results = json.loads(summaries[-1].read_text(encoding="utf-8"))
    return {
        "resolved": int(results.get("resolved_instances") or 0),
        "unresolved": int(results.get("unresolved_instances") or 0),
        "total": int(results.get("dataset_instances") or 0),
        "resolvedIds": list(results.get("resolved_ids") or []),
        "unresolvedIds": list(results.get("unresolved_ids") or []),
    }


def run_baseline_instance(
    definition: dict, instance_id: str, model: dict, work_root: Path, remaining: float
) -> dict:
    baseline_root = work_root / "baseline"
    baseline_root.mkdir(parents=True, exist_ok=True)
    trajectory = baseline_root / f"{model['tier']}-{safe(instance_id)}.traj.json"
    env = baseline_environment(definition, model)
    log = work_root / "logs" / f"baseline-{model['tier']}-{safe(instance_id)}.log"
    result = run_command(
        baseline_command(definition, instance_id, model, trajectory, remaining),
        cwd=baseline_root,
        env=env,
        log=log,
    )
    info = {}
    if trajectory.is_file():
        info = json.loads(trajectory.read_text(encoding="utf-8")).get("info") or {}
    stats = info.get("model_stats") or {}
    spend = stats.get("instance_cost")
    spend = float(spend) if isinstance(spend, (int, float)) and spend > 0 else None
    exit_status = info.get("exit_status")
    submission = info.get("submission") or ""
    run_id = f"baseline-{model['tier']}-{safe(instance_id)}"
    predictions = baseline_root / f"{model['tier']}-{safe(instance_id)}.predictions.jsonl"
    prediction = {
        "instance_id": instance_id,
        "model_name_or_path": model["id"],
        "model_patch": submission,
    }
    predictions.write_text(json.dumps(prediction) + "\n", encoding="utf-8", newline="\n")
    eval_command = [
        definition["_eval_python"],
        "-m",
        "swebench.harness.run_evaluation",
        "--dataset_name",
        definition["dataset"],
        "--split",
        definition["split"],
        "--instance_ids",
        instance_id,
        "--predictions_path",
        str(predictions),
        "--max_workers",
        "1",
        "--run_id",
        run_id,
        "--timeout",
        str(definition["instanceTimeoutSeconds"]),
    ]
    eval_log = work_root / "logs" / f"baseline-eval-{model['tier']}-{safe(instance_id)}.log"
    eval_result = run_command(eval_command, cwd=baseline_root, env=os.environ.copy(), log=eval_log)
    counts = baseline_counts(baseline_root, run_id)
    resolved = bool(counts and instance_id in (counts.get("resolvedIds") or []))
    failure = None
    if result.returncode != 0 or not trajectory.is_file():
        failure = "baseline run failed"
    elif exit_status and exit_status != "Submitted":
        failure = f"baseline {exit_status}"
    elif not submission:
        failure = "empty patch"
    elif eval_result.returncode != 0 or counts is None:
        failure = "official evaluation missing"
    elif not resolved:
        failure = "unresolved"
    return {
        "instanceId": instance_id,
        "exitCode": result.returncode,
        "completed": result.returncode == 0,
        "hasPatch": bool(submission),
        "resolved": resolved,
        "spendUsd": spend,
        "steps": int(stats.get("api_calls") or 0),
        "inputTokens": 0,
        "outputTokens": 0,
        "limit": None,
        "exitStatus": exit_status,
        "failure": failure,
        "workDir": str(baseline_root.relative_to(REPOSITORY)) if baseline_root.is_relative_to(REPOSITORY) else str(baseline_root),
    }
