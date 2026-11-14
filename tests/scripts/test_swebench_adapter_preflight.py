import json
import shutil
import subprocess
import sys
import unittest
from pathlib import Path

from swebench_adapter_support import (
    ADAPTER,
    REPOSITORY_ROOT,
    AdapterTestCase,
    load_adapter_module,
)


class SwebenchAdapterPreflightTests(AdapterTestCase):
    def test_help_lists_the_documented_flags(self) -> None:
        result = self.run_adapter("--help")

        self.assertEqual(0, result.returncode, result.stderr)
        for flag in (
            "--dry-run",
            "--instance",
            "--model",
            "--lunate-bin",
            "--dataset-file",
            "--dataset-name",
            "--split",
            "--output-dir",
            "--run-id",
            "--evaluate",
            "--env",
            "--timeout",
            "--max-cost",
            "--max-time",
            "--max-workers",
        ):
            self.assertIn(flag, result.stdout)
        self.assertIn("ProgramBench", result.stdout)

    def test_a_missing_instance_is_a_usage_error_without_a_run_directory(self) -> None:
        result = self.run_adapter(*self.base_arguments("--dry-run"))

        self.assertEqual(2, result.returncode)
        self.assertIn("--instance", result.stderr)
        self.assertFalse(self.output.exists())

    def test_a_non_elf_binary_is_refused_before_docker(self) -> None:
        text_binary = self.tmp / "text-binary"
        text_binary.write_text("#!/bin/sh\n", encoding="utf-8")
        text_binary.chmod(0o755)
        arguments = self.base_arguments(
            "--dry-run",
            "--instance",
            "alpha__one-1",
            "--run-id",
            "run-3",
            "--env",
            "FAKE_API_KEY",
        )
        arguments[arguments.index("--lunate-bin") + 1] = str(text_binary)

        result = self.run_adapter(*arguments)

        self.assertEqual(1, result.returncode)
        self.assertIn("linux-x64 ELF", result.stderr)
        self.assertFalse(self.docker_args.exists())

    def test_a_missing_credential_stops_before_the_container(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--dry-run",
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-4",
                "--env",
                "MISSING_PROVIDER_KEY",
            ),
            environment=self.adapter_environment(MISSING_PROVIDER_KEY=""),
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("MISSING_PROVIDER_KEY", result.stderr)
        self.assertFalse(self.docker_args.exists())

    def test_a_missing_official_harness_is_an_honest_failure(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-6",
                "--env",
                "FAKE_API_KEY",
                "--evaluate",
                "always",
            ),
            environment=self.adapter_environment(with_harness=False),
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("pip install swebench", result.stderr)
        self.assertFalse(self.docker_args.exists())

    def test_the_old_dataset_image_name_is_derived(self) -> None:
        module = load_adapter_module("swebench_adapter")

        self.assertEqual(
            "swebench/sweb.eval.x86_64.alpha_1776_one-1",
            module.instance_image({"instance_id": "alpha__one-1"}),
        )
        self.assertEqual(
            "example/alpha-one:latest",
            module.instance_image({"instance_id": "alpha__one-1", "image": "example/alpha-one:latest"}),
        )

    def test_the_current_official_dataset_is_the_default(self) -> None:
        module = load_adapter_module("swebench_adapter_default")

        self.assertEqual("SWE-bench/SWE-bench_Verified", module.DEFAULT_DATASET)

    def test_the_patch_collector_keeps_new_files_and_skips_baseline_untracked(self) -> None:
        if shutil.which("git") is None:
            self.skipTest("git is not available")
        module = load_adapter_module("swebench_adapter_patch")
        repo = self.tmp / "repo"
        repo.mkdir()
        subprocess.run(["git", "-C", str(repo), "init", "-q"], check=True)
        subprocess.run(
            ["git", "-C", str(repo), "config", "user.email", "test@example.invalid"], check=True
        )
        subprocess.run(["git", "-C", str(repo), "config", "user.name", "Test"], check=True)
        (repo / "tracked.txt").write_text("before\n", encoding="utf-8")
        subprocess.run(["git", "-C", str(repo), "add", "tracked.txt"], check=True)
        subprocess.run(["git", "-C", str(repo), "commit", "-q", "-m", "initial"], check=True)
        baseline = self.tmp / "baseline.txt"
        baseline.write_text("preexisting.txt\n", encoding="utf-8")
        (repo / "preexisting.txt").write_text("image setup\n", encoding="utf-8")
        (repo / "tracked.txt").write_text("after\n", encoding="utf-8")
        (repo / "new-file.txt").write_text("agent output\n", encoding="utf-8")
        (repo / "qemu_lunate_20261108-083421_27.core").write_bytes(b"\x7fELF core")
        (repo / "core.1234").write_bytes(b"core dump")
        collector = self.tmp / "collect_patch.py"
        collector.write_text(module.PATCH_COLLECTOR, encoding="utf-8")
        destination = self.tmp / "patch.diff"

        result = subprocess.run(
            [sys.executable, str(collector), str(repo), str(baseline), str(destination)],
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        patch = destination.read_text(encoding="utf-8")
        self.assertIn("new-file.txt", patch)
        self.assertIn("tracked.txt", patch)
        self.assertNotIn("preexisting.txt", patch)
        self.assertNotIn("qemu_lunate", patch)
        self.assertNotIn("core.1234", patch)

    def test_programbench_is_documented_as_out_of_scope(self) -> None:
        documentation = (REPOSITORY_ROOT / "eval" / "README.md").read_text(encoding="utf-8")

        self.assertIn("ProgramBench", documentation)
        self.assertIn("programbench eval", documentation)
        self.assertIn("whole", documentation.lower())

    def test_the_adapter_module_imports_from_its_own_directory(self) -> None:
        result = subprocess.run(
            [sys.executable, str(ADAPTER), "--help"],
            capture_output=True,
            text=True,
            check=False,
            cwd=self.tmp,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("--dry-run", result.stdout)


if __name__ == "__main__":
    unittest.main()
