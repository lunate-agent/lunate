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
from swebench_baseline import run_baseline_instance
from swebench_process import instance_image, run_command, safe, utc_now

REPOSITORY = Path(__file__).resolve().parents[2]
ADAPTER = REPOSITORY / "scripts" / "eval" / "swebench.py"
CATALOG = REPOSITORY / "src" / "Lunate.Ai" / "models.json"
SWEBENCH_DIR = REPOSITORY / "eval" / "swebench"


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
    parser.add_argument("--resume", action="store_true", help="reuse completed Lunate instance runs")
    parser.add_argument("--keep-images", action="store_true", help="do not remove an instance image afterwards")
    return parser.parse_args(argv)


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


def read_lunate_row(
    definition: dict, instance_id: str, model: dict, run_dir: Path, returncode: int
) -> dict:
    run_json = {}
    if (run_dir / "run.json").is_file():
        run_json = json.loads((run_dir / "run.json").read_text(encoding="utf-8"))
    totals = trajectory_totals(run_dir / instance_id / "trajectory.jsonl")
    pricing = model_pricing(CATALOG, model["id"])
    spend = instance_spend(totals, pricing)
    counts = official_counts(run_dir)
    instance = (run_json.get("instances") or [{}])[0]
    exit_code = int(instance.get("exitCode", returncode))
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
    return read_lunate_row(definition, instance_id, model, run_dir, result.returncode)


def run_arm(
    definition: dict, subset: list[str], arm: str, work_root: Path, initial_spent: float = 0.0
) -> dict:
    models = definition["models"]
    cap = float(definition["limits"]["maxCostUsd"])
    spent = initial_spent
    arms = []
    rows_by_tier = {model["tier"]: [] for model in models}
    cap_reached = False
    unknown_spend = False
    for instance_id in subset:
        if spent >= cap:
            cap_reached = True
            break
        runner = run_lunate_instance if arm == "lunate" else run_baseline_instance
        rows_by_model: dict[str, dict] = {}
        pending: list[dict] = []
        for model in models:
            run_dir = work_root / "lunate" / f"{model['tier']}-{safe(instance_id)}"
            if arm == "lunate" and definition.get("_resume") and (run_dir / "run.json").is_file():
                rows_by_model[model["tier"]] = read_lunate_row(definition, instance_id, model, run_dir, 0)
            else:
                if run_dir.exists():
                    shutil.rmtree(run_dir)
                pending.append(model)
        if pending:
            remaining = max(cap - spent - sum(row["spendUsd"] or 0.0 for row in rows_by_model.values()), 0.0)
            per_model = remaining / len(pending)
            with ThreadPoolExecutor(max_workers=len(pending)) as executor:
                futures = [
                    executor.submit(runner, definition, instance_id, model, work_root, per_model)
                    for model in pending
                ]
                for model, future in zip(pending, futures):
                    rows_by_model[model["tier"]] = future.result()
        rows = [rows_by_model[model["tier"]] for model in models]
        for row in rows:
            if row.get("spendUsd") is None:
                unknown_spend = True
            else:
                spent += row["spendUsd"]
        for model, row in zip(models, rows):
            rows_by_tier[model["tier"]].append(row)
        if not definition.get("_keep_images"):
            image = instance_image(instance_id)
            subprocess.run([definition["_docker"], "rmi", "-f", image], capture_output=True, text=True, check=False)
        if unknown_spend:
            cap_reached = True
            break
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
        "totalSpendUsd": None if unknown_spend else round(spent, 6),
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
    totals = [lunate["totalSpendUsd"]]
    if baseline:
        totals.append(baseline["totalSpendUsd"])
    total_spend = None if any(total is None for total in totals) else round(totals[-1], 6)
    summary = {
        "schemaVersion": 1,
        "runId": run_id,
        "dataset": lunate["dataset"],
        "split": lunate["split"],
        "subset": lunate["subset"],
        "total": lunate["total"],
        "maxCostUsd": lunate["maxCostUsd"],
        "totalSpendUsd": total_spend,
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
            "_resume": args.resume,
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
        if previous_spent is None:
            print(
                "swebench-run: the Lunate arm has an unpriced instance, so the cap cannot be "
                "certified; not starting the baseline",
                file=sys.stderr,
            )
        else:
            arm = run_arm(definition, subset, "baseline", work_root, initial_spent=previous_spent)
            (summary_dir / f"{args.run_id}.baseline.json").write_text(
                json.dumps(arm, indent=2) + "\n", encoding="utf-8", newline="\n"
            )
    if args.arm in ("merge", "all"):
        merge(summary_dir, args.run_id)
    return 0


if __name__ == "__main__":
    sys.exit(main())
