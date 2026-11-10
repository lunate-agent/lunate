import json
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY / "scripts" / "eval"))

from swebench_run_support import (  # noqa: E402
    instance_spend,
    load_definition,
    load_subset,
    official_counts,
    priced_spend,
    render_markdown,
    summarize_arm,
    trajectory_totals,
)
from swebench_baseline import baseline_command  # noqa: E402
import swebench_run  # noqa: E402

EVAL = REPOSITORY / "eval" / "swebench"


class SwebenchRunDefinitionTests(unittest.TestCase):
    def test_the_pinned_subset_is_the_stride_rule(self) -> None:
        subset = load_subset(EVAL / "instances.txt")
        official = [
            line.strip()
            for line in (REPOSITORY / "tests" / "fixtures" / "swebench" / "verified-test-ids.txt")
            .read_text(encoding="utf-8")
            .splitlines()
            if line.strip() and not line.strip().startswith("#")
        ]

        self.assertEqual(500, len(official))
        self.assertEqual(
            [official[index] for index in (0, 100, 200, 300, 400)],
            subset,
        )

    def test_the_pinned_models_are_one_budget_and_one_frontier(self) -> None:
        definition = load_definition(EVAL / "run.json")

        tiers = sorted(model["tier"] for model in definition["models"])
        self.assertEqual(["budget", "frontier"], tiers)
        self.assertEqual("SWE-bench/SWE-bench_Verified", definition["dataset"])
        self.assertGreater(definition["limits"]["maxCostUsd"], 0)

    def test_a_definition_without_both_tiers_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "run.json"
            path.write_text(
                json.dumps(
                    {
                        "dataset": "SWE-bench/SWE-bench_Verified",
                        "split": "test",
                        "instancesFile": "instances.txt",
                        "limits": {"maxCostUsd": 5},
                        "models": [{"tier": "budget", "id": "one"}],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(ValueError):
                load_definition(path)

    def test_a_duplicate_instance_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "instances.txt"
            path.write_text("# comment\nalpha__one-1\nalpha__one-1\n", encoding="utf-8")

            with self.assertRaises(ValueError):
                load_subset(path)

    def test_the_baseline_command_carries_the_registry_and_session_header(self) -> None:
        definition = load_definition(EVAL / "run.json")

        command = baseline_command(
            definition,
            "alpha__one-1",
            definition["models"][0],
            Path("trajectory.json"),
            5.0,
        )

        self.assertIn("swebench-single", command)
        self.assertIn("--exit-immediately", command)
        self.assertIn("swebench.yaml", command)
        self.assertTrue(any("mini-swe-agent.yaml" in part for part in command))
        self.assertTrue(any("model.litellm_model_registry=" in part for part in command))
        self.assertTrue(any("model.model_kwargs.api_base=" in part for part in command))
        self.assertIn("agent.step_limit=50", command)
        self.assertIn("agent.cost_limit=3.0000", command)


class SwebenchRunSpendTests(unittest.TestCase):
    def test_trajectory_totals_and_catalog_price(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trajectory.jsonl"
            path.write_text(
                "\n".join(
                    [
                        json.dumps(
                            {
                                "type": "usage_updated",
                                "usage": {"inputTokenCount": 2_000_000, "outputTokenCount": 1000},
                                "cacheReadTokens": 1000,
                            }
                        ),
                        json.dumps({"type": "run_finished", "stopReason": "stop"}),
                    ]
                ),
                encoding="utf-8",
            )

            totals = trajectory_totals(path)
            spend = priced_spend({"input": 0.15, "output": 0.6, "cacheRead": 0.003}, totals)

            self.assertEqual(1, totals["steps"])
            self.assertEqual(2_000_000, totals["inputTokens"])
            self.assertAlmostEqual(0.300453, spend, places=9)

    def test_a_cost_limit_event_reports_its_own_spend(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trajectory.jsonl"
            path.write_text(
                json.dumps(
                    {
                        "type": "cost_limit_reached",
                        "maxCostUsd": 5,
                        "spendUsd": 5.25,
                    }
                ),
                encoding="utf-8",
            )

            totals = trajectory_totals(path)

            self.assertEqual({"kind": "cost", "maxCostUsd": 5, "spendUsd": 5.25}, totals["limit"])
            self.assertEqual(5.25, instance_spend(totals, {"input": 1}))

    def test_an_unpriced_lane_is_not_zero(self) -> None:
        totals = {
            "inputTokens": 1000,
            "outputTokens": 0,
            "cacheReadTokens": 0,
            "cacheWriteTokens": 0,
            "costUsd": 0.0,
            "steps": 1,
            "limit": None,
        }

        self.assertIsNone(priced_spend({"output": 1}, totals))


class SwebenchRunSummaryTests(unittest.TestCase):
    def test_the_summary_carries_the_official_counts_and_spend(self) -> None:
        rows = [
            {
                "instanceId": "alpha__one-1",
                "resolved": True,
                "spendUsd": 0.25,
                "failure": None,
            },
            {
                "instanceId": "beta__two-2",
                "resolved": False,
                "spendUsd": 0.5,
                "failure": "unresolved",
            },
        ]

        arm = summarize_arm("lunate", "budget", "opencode-go/model", rows)

        self.assertEqual(1, arm["resolved"])
        self.assertEqual(2, arm["total"])
        self.assertAlmostEqual(0.75, arm["spendUsd"])
        self.assertEqual("beta__two-2", arm["failures"][0]["instanceId"])

    def test_the_markdown_names_the_cap_and_the_arms(self) -> None:
        summary = {
            "runId": "run-1",
            "dataset": "SWE-bench/SWE-bench_Verified",
            "split": "test",
            "total": 1,
            "maxCostUsd": 30.0,
            "totalSpendUsd": 0.25,
            "capReached": False,
            "arms": [
                {
                    "arm": "lunate",
                    "tier": "budget",
                    "model": "opencode-go/model",
                    "resolved": 1,
                    "total": 1,
                    "spendUsd": 0.25,
                    "failures": [],
                }
            ],
        }

        markdown = render_markdown(summary)

        self.assertIn("$30.00", markdown)
        self.assertIn("$0.2500", markdown)
        self.assertIn("lunate", markdown)
        self.assertIn("1/1", markdown)

    def test_official_counts_come_from_the_harness_summary(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "official-results.json").write_text(
                json.dumps(
                    {
                        "resolved_instances": 1,
                        "unresolved_instances": 0,
                        "dataset_instances": 1,
                        "resolved_ids": ["alpha__one-1"],
                    }
                ),
                encoding="utf-8",
            )

            counts = official_counts(root)

            self.assertEqual(1, counts["resolved"])
            self.assertEqual(["alpha__one-1"], counts["resolvedIds"])


class SwebenchRunOrchestrationTests(unittest.TestCase):
    def test_the_lunate_arm_invokes_the_adapter_and_aggregates_the_spend(self) -> None:
        import subprocess

        fake_adapter = """\
import json
import sys
from pathlib import Path

args = sys.argv[1:]
def value(flag):
    return args[args.index(flag) + 1]

instance = value("--instance")
output = Path(value("--output-dir"))
run_id = value("--run-id")
run_dir = output / run_id
(run_dir / instance).mkdir(parents=True, exist_ok=True)
(run_dir / instance / "trajectory.jsonl").write_text(
    json.dumps({
        "type": "usage_updated",
        "usage": {"inputTokenCount": 1000000, "outputTokenCount": 1000},
        "cacheReadTokens": 0,
    }) + "\\n",
    encoding="utf-8",
)
(run_dir / "run.json").write_text(
    json.dumps({
        "instances": [{"instanceId": instance, "exitCode": 0, "hasPatch": True}],
        "evaluation": {"requested": True},
    }),
    encoding="utf-8",
)
(run_dir / "official-results.json").write_text(
    json.dumps({
        "resolved_instances": 1,
        "unresolved_instances": 0,
        "dataset_instances": 1,
        "resolved_ids": [instance],
    }),
    encoding="utf-8",
)
"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            subset = root / "instances.txt"
            subset.write_text("alpha__one-1\n", encoding="utf-8")
            config = root / "run.json"
            config.write_text(
                json.dumps(
                    {
                        "dataset": "SWE-bench/SWE-bench_Verified",
                        "split": "test",
                        "instancesFile": "instances.txt",
                        "limits": {"maxCostUsd": 10, "maxTimeSeconds": 60},
                        "instanceTimeoutSeconds": 60,
                        "models": [
                            {
                                "tier": "budget",
                                "id": "opencode-go/deepseek-v4.1-flash",
                                "credential": "FAKE_KEY",
                            },
                            {
                                "tier": "frontier",
                                "id": "openrouter/anthropic/claude-opus-5.5",
                                "credential": "FAKE_KEY",
                            },
                        ],
                    }
                ),
                encoding="utf-8",
            )
            adapter = root / "fake_adapter.py"
            adapter.write_text(fake_adapter, encoding="utf-8")
            binary = root / "lunate"
            binary.write_bytes(b"\x7fELF" + b"\x00" * 64)
            binary.chmod(0o755)
            work = root / "work"
            summary = root / "summary"

            result = subprocess.run(
                [
                    sys.executable,
                    str(REPOSITORY / "scripts" / "eval" / "swebench_run.py"),
                    "--config",
                    str(config),
                    "--work-dir",
                    str(work),
                    "--summary-dir",
                    str(summary),
                    "--run-id",
                    "run-1",
                    "--arm",
                    "lunate",
                    "--adapter",
                    str(adapter),
                    "--lunate-bin",
                    str(binary),
                ],
                capture_output=True,
                text=True,
                check=False,
                env={**__import__("os").environ, "FAKE_KEY": "placeholder"},
            )

            self.assertEqual(0, result.returncode, result.stderr)
            arm = json.loads((summary / "run-1.lunate.json").read_text(encoding="utf-8"))
            self.assertEqual(2, len(arm["arms"]))
            self.assertEqual(1, arm["arms"][0]["resolved"])
            self.assertAlmostEqual(4.1706, arm["totalSpendUsd"], places=6)

            marker = root / "invoked"
            adapter.write_text(
                "import sys\nfrom pathlib import Path\n"
                f"Path({str(marker)!r}).write_text('invoked', encoding='utf-8')\n"
                "sys.exit(1)\n",
                encoding="utf-8",
            )
            resumed = subprocess.run(
                [
                    sys.executable,
                    str(REPOSITORY / "scripts" / "eval" / "swebench_run.py"),
                    "--config",
                    str(config),
                    "--work-dir",
                    str(work),
                    "--summary-dir",
                    str(summary),
                    "--run-id",
                    "run-1",
                    "--arm",
                    "lunate",
                    "--resume",
                    "--adapter",
                    str(adapter),
                    "--lunate-bin",
                    str(binary),
                ],
                capture_output=True,
                text=True,
                check=False,
                env={**__import__("os").environ, "FAKE_KEY": "placeholder"},
            )

            self.assertEqual(0, resumed.returncode, resumed.stderr)
            self.assertFalse(marker.exists())
            resumed_arm = json.loads((summary / "run-1.lunate.json").read_text(encoding="utf-8"))
            self.assertEqual(1, resumed_arm["arms"][0]["resolved"])


class SwebenchRunCapTests(unittest.TestCase):
    def definition(self, cap: float = 30.0) -> dict:
        return {
            "_run_id": "run-1",
            "_started_at": "2026-11-10T00:00:00Z",
            "dataset": "SWE-bench/SWE-bench_Verified",
            "split": "test",
            "limits": {"maxCostUsd": cap, "maxTimeSeconds": 60},
            "models": [
                {"tier": "budget", "id": "opencode-go/budget"},
                {"tier": "frontier", "id": "opencode-go/frontier"},
            ],
            "_keep_images": True,
            "_resume": False,
        }

    def test_an_unpriced_instance_stops_the_run_and_leaves_the_total_unknown(self) -> None:
        from unittest import mock

        calls: list[tuple[str, str]] = []

        def runner(definition, instance_id, model, work_root, remaining):
            calls.append((instance_id, model["tier"]))
            return {
                "instanceId": instance_id,
                "resolved": False,
                "spendUsd": None if model["tier"] == "budget" else 0.5,
                "failure": None,
            }

        with tempfile.TemporaryDirectory() as directory:
            with mock.patch.object(swebench_run, "run_lunate_instance", side_effect=runner):
                arm = swebench_run.run_arm(
                    self.definition(), ["alpha__one-1", "beta__two-2"], "lunate", Path(directory)
                )

        self.assertEqual(
            {("alpha__one-1", "budget"), ("alpha__one-1", "frontier")}, set(calls)
        )
        self.assertIsNone(arm["totalSpendUsd"])
        self.assertTrue(arm["capReached"])
        self.assertEqual([1, 1], [len(item["instances"]) for item in arm["arms"]])

    def test_merge_does_not_turn_an_unknown_total_into_a_number(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "run-1.lunate.json").write_text(
                json.dumps(
                    {
                        "runId": "run-1",
                        "dataset": "SWE-bench/SWE-bench_Verified",
                        "split": "test",
                        "subset": ["alpha__one-1"],
                        "total": 1,
                        "maxCostUsd": 30.0,
                        "totalSpendUsd": None,
                        "capReached": True,
                        "startedAt": "2026-11-10T00:00:00Z",
                        "finishedAt": "2026-11-10T00:01:00Z",
                        "arms": [],
                    }
                ),
                encoding="utf-8",
            )

            swebench_run.merge(root, "run-1")

            summary = json.loads((root / "summary.json").read_text(encoding="utf-8"))
            self.assertIsNone(summary["totalSpendUsd"])
            self.assertIn("unknown", (root / "summary.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
