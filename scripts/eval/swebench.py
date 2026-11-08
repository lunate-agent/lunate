#!/usr/bin/env python3
"""Run Lunate on SWE-bench instances and hand the patches to the official evaluation.

The adapter is the reproducible entry point behind the SWE-bench claim: it starts the
official per-instance image, runs `lunate -p --json --yolo` in the image's `/testbed`,
keeps the event stream as the trajectory and the working-tree diff as the patch, then
invokes `swebench.harness.run_evaluation`. It never scores a run itself.

Prerequisites: Docker with a reachable daemon, a self-contained `linux-x64` Lunate
single-file build, a provider credential in the environment, and (unless
`--evaluate never`) the official `swebench` package in `--eval-python`.

Examples:
  python3 scripts/eval/swebench.py --help

  python3 scripts/eval/swebench.py --dry-run \\
      --instance astropy__astropy-12907 \\
      --model deepseek/deepseek-v4.1-flash \\
      --lunate-bin artifacts/linux-x64/lunate

  python3 scripts/eval/swebench.py \\
      --instance astropy__astropy-12907 django__django-11099 \\
      --model deepseek/deepseek-v4.1-flash \\
      --lunate-bin artifacts/linux-x64/lunate \\
      --run-id verified-2026-11-08
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from swebench_artifacts import redact_tree, write_run_json
from swebench_execution import PATCH_COLLECTOR, evaluate, run_instance, write_predictions
from swebench_support import (
    DEFAULT_DATASET,
    PUBLISH_COMMAND,
    AdapterError,
    check_binary,
    check_docker,
    check_official_harness,
    check_usage,
    forwarded_environment,
    instance_image,
    load_dataset,
    select_instances,
    utc_now,
)

__all__ = ["PATCH_COLLECTOR", "execute", "main", "parse_args"]


def parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run Lunate on SWE-bench instance images and call the official evaluation.",
        epilog=(
            "Prerequisites: Docker with a reachable daemon; a self-contained linux-x64 Lunate "
            "single-file build (publish with: " + PUBLISH_COMMAND + "); at least one provider "
            "credential in the environment; the official swebench package in --eval-python when "
            "evaluation runs. ProgramBench is not covered: its images have no git /testbed and its "
            "submission is a whole-codebase tarball scored by `programbench eval`."
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--instance",
        action="append",
        default=[],
        metavar="ID",
        help="instance id to run (repeatable; required unless --dry-run)",
    )
    parser.add_argument(
        "--dry-run", action="store_true", help="run exactly one instance; evaluation defaults to never"
    )
    parser.add_argument("--model", metavar="ID", help="exact Lunate model id, passed to `lunate -p --model`")
    parser.add_argument(
        "--lunate-bin", metavar="PATH", help="self-contained linux-x64 single-file Lunate executable"
    )
    parser.add_argument(
        "--dataset-file",
        metavar="PATH",
        help="local SWE-bench JSON array or JSONL file (offline runs)",
    )
    parser.add_argument(
        "--dataset-name",
        default=DEFAULT_DATASET,
        metavar="NAME",
        help=f"official dataset name (default: {DEFAULT_DATASET})",
    )
    parser.add_argument("--split", default="test", metavar="SPLIT", help="dataset split (default: test)")
    parser.add_argument(
        "--output-dir",
        default="eval/runs/swebench",
        metavar="DIR",
        help="run root parent (default: eval/runs/swebench)",
    )
    parser.add_argument("--run-id", metavar="ID", help="run directory name (default: a UTC timestamp)")
    parser.add_argument(
        "--evaluate",
        choices=("auto", "always", "never"),
        help="evaluation mode; auto outside --dry-run, never inside it",
    )
    parser.add_argument(
        "--env",
        action="append",
        default=[],
        metavar="NAME",
        help="provider credential variable to forward by name (repeatable)",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=1800,
        metavar="SECONDS",
        help="container and official-test timeout (default: 1800)",
    )
    parser.add_argument(
        "--eval-python",
        default=sys.executable,
        metavar="PYTHON",
        help="interpreter with the official swebench package (default: this Python)",
    )
    parser.add_argument("--docker", default="docker", metavar="PATH", help="Docker client (default: docker)")
    parser.add_argument("--keep", action="store_true", help="keep the instance container for debugging")
    return parser.parse_args(argv)


def execute(args: argparse.Namespace) -> int:
    check_usage(args)
    evaluate_mode = args.evaluate or ("never" if args.dry_run else "auto")
    run_id = args.run_id or "swebench-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    args.run_id = run_id
    forwarded = forwarded_environment(args.env)
    args.lunate_bin = check_binary(args.lunate_bin)
    check_docker(args)
    records = load_dataset(args)
    selected = select_instances(records, args.instance)
    check_official_harness(args, evaluate_mode)
    run_root = Path(args.output_dir).resolve() / run_id
    if run_root.exists() and any(run_root.iterdir()):
        raise AdapterError(f"run directory '{run_root}' already exists; pass a different --run-id")
    run_root.mkdir(parents=True, exist_ok=True)
    (run_root / "dataset.json").write_text(
        json.dumps(selected, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n"
    )
    payload = {
        "runId": run_id,
        "startedAt": utc_now(),
        "dryRun": args.dry_run,
        "model": args.model,
        "dataset": args.dataset_name,
        "datasetFile": args.dataset_file,
        "split": args.split,
        "lunateBinary": str(args.lunate_bin),
        "forwardedEnvironment": sorted(forwarded),
        "evaluate": evaluate_mode,
        "instances": [],
        "evaluation": {"requested": False},
    }
    print(f"run root: {run_root}", file=sys.stderr)
    results: list[dict] = []
    for instance in selected:
        instance_id = str(instance["instance_id"])
        run_dir = run_root / instance_id
        run_dir.mkdir(parents=True, exist_ok=True)
        print(f"running {instance_id} in {instance_image(instance)}", file=sys.stderr)
        result = run_instance(args, instance, run_dir, forwarded)
        results.append(result)
        payload["instances"].append(result)
        print(
            f"{instance_id}: exit={result['exitCode']} patch={result['hasPatch']} "
            f"trajectory={result['hasTrajectory']}",
            file=sys.stderr,
        )
    predictions = write_predictions(run_root, results, args.model)
    evaluation = {"requested": False, "reason": "evaluation disabled"}
    if evaluate_mode != "never":
        ids = [result["instanceId"] for result in results if result["hasPatch"]]
        if ids:
            evaluation = evaluate(args, run_root, predictions, run_root / "dataset.json", ids)
        else:
            evaluation = {"requested": False, "reason": "no non-empty patch to evaluate"}
    payload["evaluation"] = evaluation
    payload["finishedAt"] = utc_now()
    payload["secretsRedactedFiles"] = redact_tree(run_root, forwarded)
    write_run_json(run_root, payload)
    failed = [result for result in results if not result["completed"] or not result["hasTrajectory"]]
    if failed:
        print(
            "failed instances: "
            + ", ".join(f"{item['instanceId']} (exit {item['exitCode']})" for item in failed),
            file=sys.stderr,
        )
        return 1
    if args.dry_run and not all(result["hasPatch"] for result in results):
        print("dry run finished without a patch; see the trajectory and log", file=sys.stderr)
        return 1
    if evaluation.get("requested") and evaluation.get("exitCode", 1) != 0:
        print("official evaluation failed; see evaluation.log", file=sys.stderr)
        return 1
    return 0


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        return execute(args)
    except AdapterError as error:
        print(f"swebench: {error}", file=sys.stderr)
        return 2 if str(error).startswith("usage:") else 1
    except (json.JSONDecodeError, OSError) as error:
        print(f"swebench: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
