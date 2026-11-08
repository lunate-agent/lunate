"""Container execution and official-evaluation handoff for the SWE-bench adapter.

The module starts the official instance image, writes the patch and trajectory artifacts, and
invokes ``swebench.harness.run_evaluation``. It contains no scoring logic.
"""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

from swebench_support import fail, instance_image, run_command, utc_now

PATCH_COLLECTOR = '''\
import subprocess
import sys
from pathlib import Path

repo, baseline_path, destination = sys.argv[1:4]


def git(*args: str) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(["git", "-C", repo, *args], capture_output=True, check=False)


baseline_path = Path(baseline_path)
baseline = set(baseline_path.read_text(encoding="utf-8", errors="replace").splitlines()) if baseline_path.exists() else set()
untracked = [
    line
    for line in git("ls-files", "--others", "--exclude-standard").stdout.decode(errors="replace").splitlines()
    if line and line not in baseline
]
if untracked:
    git("add", "-N", "--", *untracked)
diff = git("diff", "HEAD", "--binary")
Path(destination).write_bytes(diff.stdout)
sys.exit(diff.returncode)
'''
CONTAINER_SCRIPT = """\
set -u
cd /testbed || exit 90
export LUNATE_HOME=/lunate-run/lunate-home
git ls-files --others --exclude-standard > /lunate-run/baseline-untracked.txt 2>/dev/null || true
/opt/lunate/lunate -p --json --yolo --model "$LUNATE_MODEL" "$(cat /lunate-run/prompt.md)" \\
    > /lunate-run/trajectory.jsonl 2> /lunate-run/lunate.log
code=$?
echo "$code" > /lunate-run/exit-code
python3 /lunate-run/collect_patch.py /testbed /lunate-run/baseline-untracked.txt \\
    /lunate-run/patch.diff >> /lunate-run/lunate.log 2>&1 || true
exit 0
"""


def ensure_image(args, image: str, run_dir: Path) -> None:
    inspect = run_command([args.docker, "image", "inspect", image], timeout=60)
    if inspect.returncode == 0:
        return
    print(f"pulling {image}", file=sys.stderr)
    pulled = run_command(
        [args.docker, "pull", "--platform", "linux/amd64", image], timeout=args.timeout
    )
    (run_dir / "image-pull.log").write_text(
        pulled.stdout + pulled.stderr, encoding="utf-8", newline="\n"
    )
    if pulled.returncode != 0:
        fail(
            f"could not pull official image '{image}'; build it with the official "
            "swebench harness or check Docker connectivity"
        )


def container_name(instance_id: str, run_id: str) -> str:
    safe = "".join(
        character if character.isalnum() or character in "_.-" else "_" for character in instance_id
    )
    name = f"lunate-swebench-{safe}-{run_id}"
    return name[:63]


def run_instance(args, instance: dict, run_dir: Path, forwarded: dict[str, str]) -> dict:
    instance_id = str(instance["instance_id"])
    image = instance_image(instance)
    ensure_image(args, image, run_dir)
    (run_dir / "prompt.md").write_text(
        str(instance["problem_statement"]).rstrip() + "\n", encoding="utf-8", newline="\n"
    )
    (run_dir / "run.sh").write_text(CONTAINER_SCRIPT, encoding="utf-8", newline="\n")
    (run_dir / "collect_patch.py").write_text(PATCH_COLLECTOR, encoding="utf-8", newline="\n")
    name = container_name(instance_id, args.run_id)
    run_command([args.docker, "rm", "-f", name], timeout=60)
    command = [args.docker, "run"]
    if not args.keep:
        command.append("--rm")
    command += [
        "--name",
        name,
        "--platform",
        "linux/amd64",
        "--workdir",
        "/testbed",
        "--volume",
        f"{run_dir}:/lunate-run",
        "--volume",
        f"{args.lunate_bin}:/opt/lunate/lunate:ro",
        "--env",
        f"LUNATE_MODEL={args.model}",
    ]
    for variable in forwarded:
        command += ["--env", variable]
    command += ["--entrypoint", "/bin/bash", image, "-lc", "bash /lunate-run/run.sh"]
    started = utc_now()
    try:
        result = subprocess.run(
            command,
            capture_output=True,
            text=True,
            check=False,
            timeout=args.timeout,
        )
        container_stderr = result.stderr
        client_code = result.returncode
    except subprocess.TimeoutExpired:
        run_command([args.docker, "kill", name], timeout=60)
        container_stderr = f"container exceeded {args.timeout}s and was killed"
        client_code = 124
    (run_dir / "docker.log").write_text(container_stderr, encoding="utf-8", newline="\n")
    exit_file = run_dir / "exit-code"
    try:
        lunate_exit = int(exit_file.read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        lunate_exit = client_code
    patch = run_dir / "patch.diff"
    trajectory = run_dir / "trajectory.jsonl"
    return {
        "instanceId": instance_id,
        "image": image,
        "startedAt": started,
        "finishedAt": utc_now(),
        "exitCode": lunate_exit,
        "completed": lunate_exit == 0,
        "patchPath": f"{instance_id}/patch.diff",
        "trajectoryPath": f"{instance_id}/trajectory.jsonl",
        "hasPatch": patch.is_file() and patch.stat().st_size > 0,
        "hasTrajectory": trajectory.is_file() and trajectory.stat().st_size > 0,
        "container": name,
        "containerClientExit": client_code,
    }


def write_predictions(run_root: Path, results: list[dict], model: str) -> Path:
    path = run_root / "predictions.jsonl"
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for result in results:
            instance_id = result["instanceId"]
            patch_path = run_root / result["patchPath"]
            patch = patch_path.read_text(encoding="utf-8", errors="replace") if patch_path.is_file() else ""
            record = {
                "instance_id": instance_id,
                "model_name_or_path": model,
                "model_patch": patch,
            }
            handle.write(json.dumps(record, ensure_ascii=False) + "\n")
    return path


def evaluation_command(args, predictions: Path, dataset: Path, ids: list[str]) -> list[str]:
    return [
        args.eval_python,
        "-m",
        "swebench.harness.run_evaluation",
        "--dataset_name",
        str(dataset),
        "--split",
        args.split,
        "--instance_ids",
        *ids,
        "--predictions_path",
        str(predictions),
        "--max_workers",
        "1",
        "--run_id",
        args.run_id,
        "--timeout",
        str(args.timeout),
    ]


def evaluate(args, run_root: Path, predictions: Path, dataset: Path, ids: list[str]) -> dict:
    command = evaluation_command(args, predictions, dataset, ids)
    result = subprocess.run(command, capture_output=True, text=True, check=False, cwd=run_root)
    (run_root / "evaluation.log").write_text(
        result.stdout + result.stderr, encoding="utf-8", newline="\n"
    )
    record = {
        "requested": True,
        "command": command,
        "exitCode": result.returncode,
        "logPath": "evaluation.log",
        "reportPaths": {},
        "resultsPath": None,
    }
    reports = sorted(run_root.rglob("report.json"), key=lambda item: item.stat().st_mtime)
    results = sorted(run_root.rglob("results.json"), key=lambda item: item.stat().st_mtime)
    if results:
        destination = run_root / "official-results.json"
        destination.write_bytes(results[-1].read_bytes())
        record["resultsPath"] = "official-results.json"
    for instance_id in ids:
        matching = [report for report in reports if report.parent.name == instance_id]
        if not matching:
            matching = [report for report in reports if instance_id in str(report)]
        if not matching:
            continue
        destination = run_root / instance_id / "official-report.json"
        destination.write_bytes(matching[-1].read_bytes())
        record["reportPaths"][instance_id] = f"{instance_id}/official-report.json"
        try:
            report = json.loads(destination.read_text(encoding="utf-8"))
            entry = report.get(instance_id, {})
            if "resolved" in entry:
                print(
                    f"official evaluation: {instance_id} resolved={entry['resolved']}",
                    file=sys.stderr,
                )
        except (OSError, json.JSONDecodeError):
            pass
    return record
