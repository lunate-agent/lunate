import json
import unittest

from swebench_adapter_support import SECRET, AdapterTestCase


class SwebenchAdapterRunTests(AdapterTestCase):
    def test_dry_run_writes_patch_and_trajectory_and_redacts_the_key(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--dry-run",
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-1",
                "--env",
                "FAKE_API_KEY",
            )
        )

        self.assertEqual(0, result.returncode, result.stderr)
        instance = self.run_root() / "alpha__one-1"
        self.assertTrue((instance / "trajectory.jsonl").is_file())
        self.assertTrue((instance / "patch.diff").is_file())
        self.assertIn("[REDACTED:FAKE_API_KEY]", (instance / "trajectory.jsonl").read_text())
        self.assertIn("[REDACTED:FAKE_API_KEY]", (instance / "lunate.log").read_text())
        run = json.loads((self.run_root() / "run.json").read_text())
        self.assertEqual(["FAKE_API_KEY"], run["forwardedEnvironment"])
        self.assertEqual(1, len(run["instances"]))
        self.assertTrue(run["instances"][0]["hasPatch"])
        self.assertTrue(run["instances"][0]["hasTrajectory"])
        self.assertGreaterEqual(run["secretsRedactedFiles"], 2)
        for path in self.run_root().rglob("*"):
            if path.is_file():
                self.assertNotIn(SECRET.encode(), path.read_bytes())

    def test_container_gets_the_testbed_and_the_variable_name_only(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--dry-run",
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-2",
                "--env",
                "FAKE_API_KEY",
            )
        )

        self.assertEqual(0, result.returncode, result.stderr)
        arguments = json.loads(self.docker_args.read_text())
        self.assertEqual("/testbed", arguments[arguments.index("--workdir") + 1])
        self.assertIn("--platform", arguments)
        self.assertEqual("linux/amd64", arguments[arguments.index("--platform") + 1])
        self.assertIn("LUNATE_MODEL=example/model", arguments)
        self.assertIn("FAKE_API_KEY", arguments)
        self.assertNotIn(SECRET, json.dumps(arguments))
        environment = (self.docker_args.parent / (self.docker_args.name + ".envs")).read_text()
        self.assertIn("FAKE_API_KEY", environment)
        self.assertNotIn(SECRET, environment)

    def test_the_official_dataset_loader_is_used_without_a_local_file(self) -> None:
        arguments = self.base_arguments(
            "--dry-run",
            "--instance",
            "alpha__one-1",
            "--run-id",
            "run-9",
            "--env",
            "FAKE_API_KEY",
        )
        arguments = [
            argument for argument in arguments if argument not in ("--dataset-file", str(self.dataset))
        ]

        result = self.run_adapter(*arguments)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue((self.run_root("run-9") / "alpha__one-1" / "trajectory.jsonl").is_file())

    def test_the_official_harness_receives_the_prediction_and_its_report_is_kept(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-5",
                "--env",
                "FAKE_API_KEY",
                "--evaluate",
                "always",
            )
        )

        self.assertEqual(0, result.returncode, result.stderr)
        prediction = json.loads(
            (self.run_root("run-5") / "predictions.jsonl").read_text().splitlines()[0]
        )
        self.assertEqual("alpha__one-1", prediction["instance_id"])
        self.assertEqual("example/model", prediction["model_name_or_path"])
        self.assertIn("+42", prediction["model_patch"])
        run = json.loads((self.run_root("run-5") / "run.json").read_text())
        command = run["evaluation"]["command"]
        self.assertEqual("swebench.harness.run_evaluation", command[command.index("-m") + 1])
        self.assertTrue(run["evaluation"]["requested"])
        self.assertEqual(0, run["evaluation"]["exitCode"])
        self.assertTrue((self.run_root("run-5") / "official-results.json").is_file())
        self.assertTrue((self.run_root("run-5") / "alpha__one-1" / "official-report.json").is_file())
        self.assertIn("resolved=True", result.stderr)

    def test_a_missing_image_is_pulled(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--dry-run",
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-7",
                "--env",
                "FAKE_API_KEY",
            ),
            environment=self.adapter_environment(FAKE_IMAGE_MISSING="1"),
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue((self.run_root("run-7") / "alpha__one-1" / "image-pull.log").is_file())
        self.assertIn("pulling example/alpha-one:latest", result.stderr)

    def test_a_non_zero_lunate_exit_is_an_honest_failure(self) -> None:
        result = self.run_adapter(
            *self.base_arguments(
                "--dry-run",
                "--instance",
                "alpha__one-1",
                "--run-id",
                "run-8",
                "--env",
                "FAKE_API_KEY",
            ),
            environment=self.adapter_environment(FAKE_EXIT="2"),
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("exit 2", result.stderr)
        run = json.loads((self.run_root("run-8") / "run.json").read_text())
        self.assertEqual(2, run["instances"][0]["exitCode"])
        self.assertFalse(run["instances"][0]["completed"])


if __name__ == "__main__":
    unittest.main()
