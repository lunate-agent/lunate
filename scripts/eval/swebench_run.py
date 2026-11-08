#!/usr/bin/env python3
"""Run the pinned SWE-bench subset and write the committed evidence summary.

The adapter (``scripts/eval/swebench.py``) owns one instance and the official handoff; this
orchestrator owns the run: it reads ``eval/swebench/run.json`` and ``instances.txt``, spends one
cost cap across the Lunate and mini-swe-agent arms, and writes ``summary.json`` / ``summary.md``.
It never computes a resolved count itself: every score comes from the official report.

Examples:
  python3 scripts/eval/swebench_run.py --arm lunate --lunate-bin artifacts/linux-x64/lunate
  python3 scripts/eval/swebench_run.py --arm baseline --mini-extra mini-extra
  python3 scripts/eval/swebench_run.py --arm merge
"""

from __future__ import annotations

import argparse
import json
import os
import platform
import shutil
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from swebench_run_support import (
    instance_spend,
    load_definition,
    load_subset,
    model_pricing,
    official_counts,
    render_markdown,
    summarize_arm,
    trajectory_totals,
)

REPOSITORY = Path(__file__).resolve().parents[2]
ADAPTER = REPOSITORY / "scripts" / "eval" / "swebench.py"
CATALOG = REPOSITORY / "src" / "Lunate.Ai" / "models.json"
SWEBENCH_DIR = REPOSITORY / "eval" / "swebench"


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def safe(value: str) -> str:
    return "".join(character if character.isalnum() or character in "_.-" else "_" for character in value)


def instance_image(instance_id: str) -> str:
    return "swebench/sweb.eval.x86_64." + instance_id.replace("__", "_1776_")


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--config", default=str(SWEBENCH_DIR / "run.json"))
    parser.add_argument("--work-dir", default=str(REPOSITORY / "eval" / "runs" / "swebench-first-run"))
    parser.add_argument("--summary-dir", default=str(SWEBENCH_DIR))
    parser.add_argument("--run-id", default="swebench-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ"))
    parser.add_argument("--arm", choices=("lunate", "baseline", "merge", "all"), default="all")
    parser.add_argument("--lunate-bin", help="self-contained linux-x64 Lunate executable")
    parser.add_argument("--adapter", default=str(ADAPTER), help="adapter entry point (tests may supply a fake)")
    parser.add_argument("--mini-extra", default=None, help="mini-extra executable (baseline arm)")
    parser.add_argument("--eval-python", default=sys.executable, help="interpreter with the official swebench package")
    parser.add_argument("--docker", default="docker")
    parser.add_argument("--limit", type=int, help="run only the first N instances (smoke runs)")
    parser.add_argument("--keep-images", action="store_true", help="do not remove an instance image afterwards")
    return parser.parse_args(argv)


def run_command(command: list[str], *, cwd: Path, env: dict[str, str], log: Path) -> subprocess.CompletedProcess:
    result = subprocess.run(command, capture_output=True, text=True, check=False, cwd=cwd, env=env)
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text(result.stdout + result.stderr, encoding="utf-8", newline="\n")
    return result


def adapter_command(
    definition: dict, instance_id: str, model: dict, remaining: float, output_dir: Path
) -> list[str]:
    command = [
        sys.executable,
        str(Path(definition["_adapter"]).resolve()),
        "--instance",
        instance_id,
        "--model",
        model["id"],
        "--lunate-bin",
        str(Path(definition["_lunate_bin"]).resolve()),
        "--eval-python",
        definition["_eval_python"],
        "--docker",
        definition["_docker"],
        "--dataset-name",
        definition["dataset"],
        "--split",
        definition["split"],
        "--output-dir",
        str(output_dir),
        "--run-id",
        f"{model['tier']}-{safe(instance_id)}",
        "--evaluate",
        "always",
        "--max-cost",
        f"{remaining:.4f}",
        "--max-time",
        str(definition["limits"]["maxTimeSeconds"]),
        "--max-workers",
        "1",
        "--timeout",
        str(definition["instanceTimeoutSeconds"]),
    ]
    for name in (model["credential"], "DOTNET_EnableWriteXorExecute"):
        if os.environ.get(name):
            command += ["--env", name]
    return command


def run_lunate_instance(
    definition: dict, instance_id: str, model: dict, work_root: Path, remaining: float
) -> dict:
    run_root = work_root / "lunate"
    run_root.mkdir(parents=True, exist_ok=True)
    command = adapter_command(definition, instance_id, model, remaining, run_root)
    env = os.environ.copy()
    if platform.machine() not in ("x86_64", "AMD64"):
        env["DOTNET_EnableWriteXorExecute"] = "0"
    log = work_root / "logs" / f"lunate-{model['tier']}-{safe(instance_id)}.log"
    result = run_command(command, cwd=REPOSITORY, env=env, log=log)
    run_dir = run_root / f"{model['tier']}-{safe(instance_id)}"
    run_json = {}
    if (run_dir / "run.json").is_file():
        run_json = json.loads((run_dir / "run.json").read_text(encoding="utf-8"))
    totals = trajectory_totals(run_dir / instance_id / "trajectory.jsonl")
    pricing = model_pricing(CATALOG, model["id"])
    spend = instance_spend(totals, pricing)
    counts = official_counts(run_dir)
    instance = (run_json.get("instances") or [{}])[0]
    exit_code = int(instance.get("exitCode", result.returncode))
    has_patch = bool(instance.get("hasPatch"))
    failure = None
    if totals.get("limit") and totals["limit"].get("kind") == "cost":
        failure = f"cost limit ({totals['limit'].get('spendUsd')} of {totals['limit'].get('maxCostUsd')})"
    elif exit_code != 0:
        failure = f"exit {exit_code}"
    elif not has_patch:
        failure = "empty patch"
    elif counts is None:
        failure = "official evaluation missing"
    elif instance_id not in (counts.get("resolvedIds") or []):
        failure = "unresolved"
    return {
        "instanceId": instance_id,
        "exitCode": exit_code,
        "completed": exit_code == 0,
        "hasPatch": has_patch,
        "resolved": bool(counts and instance_id in (counts.get("resolvedIds") or [])),
        "spendUsd": spend,
        "steps": totals["steps"],
        "inputTokens": totals["inputTokens"],
        "outputTokens": totals["outputTokens"],
        "limit": totals.get("limit"),
        "failure": failure,
        "workDir": str(run_dir.relative_to(REPOSITORY)) if run_dir.is_relative_to(REPOSITORY) else str(run_dir),
    }


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


def run_arm(
    definition: dict, subset: list[str], arm: str, work_root: Path, initial_spent: float = 0.0
) -> dict:
    models = definition["models"]
    cap = float(definition["limits"]["maxCostUsd"])
    spent = initial_spent
    arms = []
    rows_by_tier = {model["tier"]: [] for model in models}
    cap_reached = False
    for instance_id in subset:
        if spent >= cap:
            cap_reached = True
            break
        remaining = max(cap - spent, 0.0)
        per_model = remaining / len(models)
        runner = run_lunate_instance if arm == "lunate" else run_baseline_instance
        with ThreadPoolExecutor(max_workers=len(models)) as executor:
            futures = [
                executor.submit(runner, definition, instance_id, model, work_root, per_model)
                for model in models
            ]
            rows = [future.result() for future in futures]
        for row in rows:
            if row.get("spendUsd") is not None:
                spent += row["spendUsd"]
            else:
                spent = cap
        for model, row in zip(models, rows):
            rows_by_tier[model["tier"]].append(row)
        if not definition.get("_keep_images"):
            image = instance_image(instance_id)
            subprocess.run([definition["_docker"], "rmi", "-f", image], capture_output=True, text=True, check=False)
    for model in models:
        arms.append(summarize_arm(arm, model["tier"], model["id"], rows_by_tier[model["tier"]]))
    return {
        "runId": definition["_run_id"],
        "arm": arm,
        "startedAt": definition["_started_at"],
        "finishedAt": utc_now(),
        "dataset": definition["dataset"],
        "split": definition["split"],
        "subset": subset,
        "maxCostUsd": cap,
        "arms": arms,
        "total": len(subset),
        "totalSpendUsd": round(spent, 6),
        "capReached": cap_reached,
        "hostArchitecture": platform.machine(),
    }


def merge(summary_dir: Path, run_id: str) -> Path:
    lunate = json.loads((summary_dir / f"{run_id}.lunate.json").read_text(encoding="utf-8"))
    baseline_path = summary_dir / f"{run_id}.baseline.json"
    baseline = json.loads(baseline_path.read_text(encoding="utf-8")) if baseline_path.is_file() else None
    arms = list(lunate["arms"])
    if baseline:
        arms += baseline["arms"]
    summary = {
        "schemaVersion": 1,
        "runId": run_id,
        "dataset": lunate["dataset"],
        "split": lunate["split"],
        "subset": lunate["subset"],
        "total": lunate["total"],
        "maxCostUsd": lunate["maxCostUsd"],
        "totalSpendUsd": round(
            (baseline or lunate)["totalSpendUsd"], 6
        ),
        "capReached": lunate["capReached"] or bool(baseline and baseline.get("capReached")),
        "arms": arms,
        "lunate": {"startedAt": lunate["startedAt"], "finishedAt": lunate["finishedAt"]},
        "baseline": (
            {"startedAt": baseline["startedAt"], "finishedAt": baseline["finishedAt"]}
            if baseline
            else None
        ),
    }
    summary_path = summary_dir / "summary.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8", newline="\n")
    (summary_dir / "summary.md").write_text(render_markdown(summary), encoding="utf-8", newline="\n")
    return summary_path


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    config_path = Path(args.config)
    definition = load_definition(config_path)
    subset = load_subset(config_path.parent / definition["instancesFile"])
    if args.limit:
        subset = subset[: args.limit]
    if args.arm in ("lunate", "all") and not args.lunate_bin:
        print("swebench-run: --lunate-bin is required for the lunate arm", file=sys.stderr)
        return 2
    if args.arm in ("baseline", "all"):
        definition["_mini_extra"] = args.mini_extra or shutil.which("mini-extra") or "mini-extra"
    definition.update(
        {
            "_run_id": args.run_id,
            "_started_at": utc_now(),
            "_lunate_bin": args.lunate_bin,
            "_eval_python": args.eval_python,
            "_docker": args.docker,
            "_adapter": args.adapter,
            "_keep_images": args.keep_images,
        }
    )
    work_root = Path(args.work_dir).resolve() / args.run_id
    work_root.mkdir(parents=True, exist_ok=True)
    summary_dir = Path(args.summary_dir).resolve()
    summary_dir.mkdir(parents=True, exist_ok=True)
    if args.arm in ("lunate", "all"):
        arm = run_arm(definition, subset, "lunate", work_root)
        (summary_dir / f"{args.run_id}.lunate.json").write_text(
            json.dumps(arm, indent=2) + "\n", encoding="utf-8", newline="\n"
        )
        previous_spent = arm["totalSpendUsd"]
    else:
        lunate_path = summary_dir / f"{args.run_id}.lunate.json"
        previous_spent = (
            float(json.loads(lunate_path.read_text(encoding="utf-8"))["totalSpendUsd"])
            if lunate_path.is_file()
            else 0.0
        )
    if args.arm in ("baseline", "all"):
        arm = run_arm(definition, subset, "baseline", work_root, initial_spent=previous_spent)
        (summary_dir / f"{args.run_id}.baseline.json").write_text(
            json.dumps(arm, indent=2) + "\n", encoding="utf-8", newline="\n"
        )
    if args.arm in ("merge", "all"):
        merge(summary_dir, args.run_id)
    return 0


if __name__ == "__main__":
    sys.exit(main())
