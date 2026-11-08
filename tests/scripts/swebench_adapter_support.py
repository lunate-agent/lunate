"""Shared fixtures for the SWE-bench adapter tests.

The fake Docker executable and the fake official evaluation module keep the tests offline: no
container starts and no live API is called.
"""

import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
ADAPTER = REPOSITORY_ROOT / "scripts" / "eval" / "swebench.py"
SECRET = "sk-test-super-secret-value-123456"

FAKE_DOCKER = '''\
#!{python}
import json
import os
import sys
from pathlib import Path

args = sys.argv[1:]
if args[:1] == ["version"]:
    print("25.0.0")
    sys.exit(0)
if args[:2] == ["image", "inspect"]:
    sys.exit(1 if os.environ.get("FAKE_IMAGE_MISSING") == "1" else 0)
if args[:1] == ["pull"]:
    print("pulled", args[-1])
    sys.exit(0)
if args[:1] == ["kill"] or args[:2] == ["rm", "-f"]:
    sys.exit(0)
if args[:1] != ["run"]:
    print("fake docker: unsupported " + repr(args), file=sys.stderr)
    sys.exit(64)

log = Path(os.environ["FAKE_DOCKER_ARGS"])
log.write_text(json.dumps(args), encoding="utf-8")
mount = None
names = []
for index, value in enumerate(args):
    if value == "--volume" and args[index + 1].split(":")[1] == "/lunate-run":
        mount = Path(args[index + 1].split(":")[0])
    if value == "--env":
        names.append(args[index + 1])
(log.parent / (log.name + ".envs")).write_text("\\n".join(names), encoding="utf-8")
secret = os.environ.get("FAKE_SECRET", "")
if secret and any(secret in value for value in args):
    print("fake docker: secret value appeared in docker arguments", file=sys.stderr)
    sys.exit(65)
if mount is None:
    print("fake docker: no /lunate-run mount", file=sys.stderr)
    sys.exit(66)
mount.mkdir(parents=True, exist_ok=True)
(mount / "baseline-untracked.txt").write_text("", encoding="utf-8")
(mount / "trajectory.jsonl").write_text(
    json.dumps({{"type": "assistant_message", "text": "the key is " + secret}}) + "\\n",
    encoding="utf-8",
)
(mount / "lunate.log").write_text("diagnostic " + secret + "\\n", encoding="utf-8")
(mount / "exit-code").write_text(os.environ.get("FAKE_EXIT", "0"), encoding="utf-8")
(mount / "patch.diff").write_text(
    "diff --git a/answer.txt b/answer.txt\\nnew file mode 100644\\n"
    "--- /dev/null\\n+++ b/answer.txt\\n@@ -0,0 +1 @@\\n+42\\n",
    encoding="utf-8",
)
sys.exit(int(os.environ.get("FAKE_DOCKER_EXIT", "0")))
'''

FAKE_EVALUATION = '''\
import json
import sys
from pathlib import Path

args = sys.argv[1:]
def value(flag):
    return args[args.index(flag) + 1]

run_id = value("--run_id")
predictions = Path(value("--predictions_path"))
dataset = json.loads(Path(value("--dataset_name")).read_text(encoding="utf-8"))
records = [json.loads(line) for line in predictions.read_text(encoding="utf-8").splitlines() if line]
Path("evaluation-args.json").write_text(json.dumps(args), encoding="utf-8")
root = Path("logs") / "evaluation" / run_id
root.mkdir(parents=True, exist_ok=True)
for record in records:
    model = record["model_name_or_path"].replace("/", "__")
    instance = root / model / record["instance_id"]
    instance.mkdir(parents=True, exist_ok=True)
    (instance / "report.json").write_text(
        json.dumps({record["instance_id"]: {"resolved": True, "patch_successfully_applied": True}}),
        encoding="utf-8",
    )
model_safe = records[0]["model_name_or_path"].replace("/", "__")
Path(f"{model_safe}.{run_id}.json").write_text(
    json.dumps(
        {
            "resolved_instances": len(records),
            "unresolved_instances": 0,
            "dataset_instances": len(dataset),
            "resolved_ids": [record["instance_id"] for record in records],
        }
    ),
    encoding="utf-8",
)
print("fake official evaluation ok")
'''


def load_adapter_module(name: str):
    sys.path.insert(0, str(ADAPTER.parent))
    spec = importlib.util.spec_from_file_location(name, ADAPTER)
    if spec is None or spec.loader is None:
        raise AssertionError("could not load the adapter module")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@unittest.skipIf(os.name == "nt", "the fake Docker and Python executables are POSIX scripts")
class AdapterTestCase(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="lunate-swebench-test-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.output = self.tmp / "runs"
        self.dataset = self.tmp / "dataset.json"
        self.dataset.write_text(
            json.dumps(
                [
                    {
                        "instance_id": "alpha__one-1",
                        "problem_statement": "Fix the alpha parser.",
                        "image": "example/alpha-one:latest",
                        "repo": "alpha/one",
                        "base_commit": "0" * 40,
                    },
                    {
                        "instance_id": "beta__two-2",
                        "problem_statement": "Fix the beta writer.",
                        "image": "example/beta-two:latest",
                        "repo": "beta/two",
                        "base_commit": "1" * 40,
                    },
                ]
            ),
            encoding="utf-8",
        )
        self.lunate = self.tmp / "lunate"
        self.lunate.write_bytes(b"\x7fELF" + b"\x00" * 64)
        self.lunate.chmod(0o755)
        self.docker = self.tmp / "docker"
        self.docker.write_text(FAKE_DOCKER.format(python=sys.executable), encoding="utf-8")
        self.docker.chmod(0o755)
        self.fake_root = self.tmp / "fake-python"
        harness = self.fake_root / "swebench" / "harness"
        harness.mkdir(parents=True)
        (self.fake_root / "swebench" / "__init__.py").write_text("", encoding="utf-8")
        (harness / "__init__.py").write_text("", encoding="utf-8")
        (harness / "run_evaluation.py").write_text(FAKE_EVALUATION, encoding="utf-8")
        (self.fake_root / "datasets.py").write_text(
            "def load_dataset(name, split):\n"
            "    return [{\"instance_id\": \"alpha__one-1\", "
            "\"problem_statement\": \"Fix the alpha parser.\", "
            "\"image\": \"example/alpha-one:latest\"}]\n",
            encoding="utf-8",
        )
        self.docker_args = self.tmp / "docker-args.json"

    def adapter_environment(self, *, with_harness: bool = True, **extra: str) -> dict[str, str]:
        environment = os.environ.copy()
        for name in (
            "ANTHROPIC_API_KEY",
            "OPENAI_API_KEY",
            "OPENROUTER_API_KEY",
            "GOOGLE_API_KEY",
            "GEMINI_API_KEY",
            "DEEPSEEK_API_KEY",
            "MISTRAL_API_KEY",
            "GROQ_API_KEY",
            "XAI_API_KEY",
            "AZURE_OPENAI_API_KEY",
        ):
            environment.pop(name, None)
        environment["FAKE_DOCKER_ARGS"] = str(self.docker_args)
        environment["FAKE_SECRET"] = SECRET
        environment["FAKE_API_KEY"] = SECRET
        python_path = str(self.fake_root) if with_harness else str(self.tmp / "empty-python")
        environment["PYTHONPATH"] = python_path
        environment.update(extra)
        return environment

    def run_adapter(
        self, *arguments: str, environment: dict[str, str] | None = None
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(ADAPTER), *arguments],
            capture_output=True,
            text=True,
            check=False,
            cwd=self.tmp,
            env=environment or self.adapter_environment(),
            timeout=120,
        )

    def base_arguments(self, *extra: str) -> list[str]:
        return [
            "--model",
            "example/model",
            "--lunate-bin",
            str(self.lunate),
            "--dataset-file",
            str(self.dataset),
            "--output-dir",
            str(self.output),
            "--docker",
            str(self.docker),
            "--eval-python",
            sys.executable,
            *extra,
        ]

    def run_root(self, run_id: str = "run-1") -> Path:
        return self.output / run_id
