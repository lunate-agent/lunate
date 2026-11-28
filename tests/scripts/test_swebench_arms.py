"""The arms of the pinned SWE-bench definition: shape, prepared home and per-instance numbers.

The orchestrator is ``scripts/eval/swebench_run.py``; these tests need neither Docker nor a live
model — the three-arm execution drives a fake adapter, exactly as the definition does.
"""

import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY / "scripts" / "eval"))

from swebench_run_support import (  # noqa: E402
    arm_id,
    arm_instance_spend,
    arm_tool,
    load_definition,
    render_markdown,
    semantic_search_enabled,
    semantic_search_home_files,
    summarize_arm,
    trajectory_totals,
)
import swebench_run  # noqa: E402

EVAL = REPOSITORY / "eval" / "swebench"
MODEL = "opencode-go/deepseek-v4.1-flash"


def definition(arms: list[dict], cap: float = 4.0) -> dict:
    return {
        "dataset": "SWE-bench/SWE-bench_Verified",
        "split": "test",
        "instancesFile": "instances.txt",
        "selection": "Every 10th instance id in lexicographic order.",
        "limits": {"maxCostUsd": cap, "maxTimeSeconds": 60},
        "instanceTimeoutSeconds": 60,
        "models": arms,
    }


def lunate_arm(name: str, state: str, **extra: object) -> dict:
    arm = {
        "arm": name,
        "tool": "lunate",
        "tier": "budget",
        "id": MODEL,
        "credential": "CHAT_KEY",
        "semanticSearch": state,
    }
    arm.update(extra)
    return arm


def baseline_arm(name: str = "baseline", model: str = MODEL) -> dict:
    return {
        "arm": name,
        "tool": "mini-swe-agent",
        "tier": "budget",
        "id": model,
        "credential": "CHAT_KEY",
    }


class SwebenchArmDefinitionTests(unittest.TestCase):
    def load(self, arms: list[dict]) -> dict:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "run.json"
            path.write_text(json.dumps(definition(arms)), encoding="utf-8")
            return load_definition(path)

    def assert_refused(self, arms: list[dict], expected: str) -> None:
        with self.assertRaises(ValueError) as raised:
            self.load(arms)
        self.assertIn(expected, str(raised.exception))

    def test_the_three_arms_are_data_not_a_tier_grid(self) -> None:
        arms = [
            lunate_arm("semantic-search-off", "off"),
            lunate_arm("semantic-search-on", "on", embeddings={"id": "google/gemini-embedding-001", "credential": "EMBED_KEY"}),
            baseline_arm(),
        ]

        loaded = self.load(arms)

        self.assertEqual(["semantic-search-off", "semantic-search-on", "baseline"], [
            arm_id(arm) for arm in loaded["models"]
        ])
        self.assertEqual(["lunate", "lunate", "mini-swe-agent"], [
            arm_tool(arm) for arm in loaded["models"]
        ])
        self.assertTrue(semantic_search_enabled(loaded["models"][1]))

    def test_a_definition_written_before_the_arm_fields_still_loads(self) -> None:
        loaded = self.load(
            [
                {"tier": "budget", "id": "opencode-go/budget", "credential": "CHAT_KEY"},
                {"tier": "frontier", "id": "opencode-go/frontier", "credential": "CHAT_KEY"},
            ]
        )

        self.assertEqual(["budget", "frontier"], [arm_id(arm) for arm in loaded["models"]])
        self.assertEqual({"lunate"}, {arm_tool(arm) for arm in loaded["models"]})

    def test_an_unknown_tool_is_refused(self) -> None:
        self.assert_refused(
            [lunate_arm("semantic-search-off", "off"), {**baseline_arm(), "tool": "other-agent"}],
            "which the harness does not run",
        )

    def test_a_duplicate_arm_is_refused(self) -> None:
        self.assert_refused(
            [lunate_arm("semantic-search-off", "off"), lunate_arm("semantic-search-off", "on")],
            "twice",
        )

    def test_an_on_arm_without_an_embeddings_model_is_refused(self) -> None:
        self.assert_refused(
            [lunate_arm("semantic-search-on", "on")],
            "names no embeddings model",
        )

    def test_an_on_arm_without_an_embeddings_credential_is_refused(self) -> None:
        self.assert_refused(
            [
                lunate_arm(
                    "semantic-search-on",
                    "on",
                    embeddings={"id": "google/gemini-embedding-001"},
                )
            ],
            "embeddings model's credential variable",
        )

    def test_an_off_arm_with_an_embeddings_model_is_refused(self) -> None:
        self.assert_refused(
            [
                lunate_arm(
                    "semantic-search-off",
                    "off",
                    embeddings={"id": "google/gemini-embedding-001", "credential": "EMBED_KEY"},
                )
            ],
            "has semanticSearch off but names an embeddings model",
        )

    def test_the_tool_is_a_lunate_one(self) -> None:
        self.assert_refused(
            [{**baseline_arm(), "semanticSearch": "on"}],
            "semantic_search is a lunate tool",
        )

    def test_a_baseline_without_a_lunate_counterpart_is_refused(self) -> None:
        self.assert_refused(
            [
                lunate_arm("semantic-search-off", "off"),
                baseline_arm(model="opencode-go/other"),
            ],
            "which no lunate arm runs",
        )

    def test_a_run_without_a_lunate_arm_is_refused(self) -> None:
        self.assert_refused([baseline_arm()], "at least one lunate arm")


class SwebenchSemanticSearchHomeTests(unittest.TestCase):
    def arm(self) -> dict:
        return lunate_arm(
            "semantic-search-on",
            "on",
            embeddings={"id": "google/gemini-embedding-001", "credential": "EMBED_KEY"},
        )

    def test_the_home_enables_only_the_tool_and_binds_the_usage(self) -> None:
        home = semantic_search_home_files(self.arm())

        settings = home["settings.json"]
        self.assertEqual(["semantic_search"], settings["tools"]["extra"])
        self.assertEqual("embeddings", settings["usages"]["embeddings"])
        self.assertEqual("google/gemini-embedding-001", settings["profiles"]["embeddings"]["model"])
        entry = home["providers.json"]["providers"][0]
        self.assertEqual("env:EMBED_KEY", entry["credentialReference"])
        self.assertEqual("google", entry["kind"])
        self.assertIn("openai-chat", entry["baseUrls"])

    def test_the_home_never_carries_the_credential_value(self) -> None:
        text = json.dumps(semantic_search_home_files(self.arm()))

        self.assertNotIn("EMBED_KEY", text.replace("env:EMBED_KEY", "env:<name>"))
        self.assertIn("env:EMBED_KEY", text)

    def test_an_arm_without_the_tool_carries_no_home(self) -> None:
        with self.assertRaises(ValueError):
            semantic_search_home_files(lunate_arm("semantic-search-off", "off"))

    def test_a_model_on_an_unknown_provider_is_refused(self) -> None:
        arm = lunate_arm(
            "semantic-search-on",
            "on",
            embeddings={"id": "nowhere/embed", "credential": "EMBED_KEY"},
        )

        with self.assertRaises(ValueError) as raised:
            semantic_search_home_files(arm)

        self.assertIn("nowhere/embed", str(raised.exception))

    def test_the_home_is_written_lf_and_without_bom(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            home = swebench_run.write_semantic_search_home(Path(directory), self.arm())

            written = (home / "settings.json").read_bytes()
            providers = (home / "providers.json").read_bytes()

        self.assertNotIn(b"\r\n", written)
        self.assertNotIn(b"\r\n", providers)
        self.assertNotIn(b"\xef\xbb\xbf", written)
        self.assertEqual(["semantic_search"], json.loads(written)["tools"]["extra"])


class SwebenchArmMeasurementTests(unittest.TestCase):
    def arm(self, state: str = "on") -> dict:
        extra = (
            {"embeddings": {"id": "google/gemini-embedding-001", "credential": "EMBED_KEY"}}
            if state == "on"
            else {}
        )
        return lunate_arm(f"semantic-search-{state}", state, **extra)

    def trajectory(self, directory: str, *, priced: bool = True, search: bool = True) -> Path:
        path = Path(directory) / "trajectory.jsonl"
        records: list[dict] = [{"type": "tool_call_start", "callId": "c2", "toolName": "bash"}]
        if search:
            records = [
                {"type": "tool_call_start", "callId": "c1", "toolName": "semantic_search"},
                {"type": "tool_call_result", "callId": "c1", "isError": False, "output": "hit"},
                *records,
            ]
        for model in (MODEL, MODEL, "google/gemini-embedding-001"):
            record = {
                "type": "usage_updated",
                "modelId": model,
                "usage": {"inputTokenCount": 1000, "outputTokenCount": 10},
            }
            if priced:
                record["costUsd"] = 0.02
            records.append(record)
        path.write_text("\n".join(json.dumps(record) for record in records), encoding="utf-8")
        return path

    def test_the_search_call_count_comes_from_tool_call_starts(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            totals = trajectory_totals(
                self.trajectory(directory), None, "google/gemini-embedding-001"
            )

        self.assertEqual(1, totals["semanticSearchCalls"])
        self.assertEqual(2, totals["steps"])
        self.assertEqual(1, totals["embeddingCalls"])

    def test_an_embeddings_record_is_not_an_agent_step(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trajectory.jsonl"
            path.write_text(
                "\n".join(
                    json.dumps(record)
                    for record in (
                        {"type": "usage_updated", "modelId": "opencode-go/budget", "costUsd": 0.1},
                        {"type": "usage_updated", "modelId": "google/embed", "costUsd": 0.001},
                    )
                ),
                encoding="utf-8",
            )

            totals = trajectory_totals(path, None, "google/embed")

        self.assertEqual(1, totals["steps"])
        self.assertEqual(1, totals["embeddingCalls"])

    def test_the_on_arm_prices_chat_and_embedding_records_at_their_own_rates(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            totals = trajectory_totals(
                self.trajectory(directory), None, "google/gemini-embedding-001"
            )

        # Three records with their own stamped prices, never all at the chat model's lane price.
        self.assertAlmostEqual(0.06, arm_instance_spend(self.arm(), totals, {"input": 99}))

    def test_one_unpriced_record_makes_the_on_arm_unpriced(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            totals = trajectory_totals(
                self.trajectory(directory, priced=False), None, "google/gemini-embedding-001"
            )

        self.assertIsNone(arm_instance_spend(self.arm(), totals, {"input": 0.15}))

    def test_the_off_arm_keeps_its_lane_price_and_records_no_search_calls(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            totals = trajectory_totals(self.trajectory(directory, priced=False, search=False))

        spend = arm_instance_spend(self.arm("off"), totals, {"input": 0.15, "output": 0.6})

        self.assertAlmostEqual(3 * 1000 * 0.15 / 1_000_000 + 3 * 10 * 0.6 / 1_000_000, spend)
        self.assertEqual(0, totals["semanticSearchCalls"])

    def test_the_arm_summary_and_markdown_carry_the_search_counts_and_arm_name(self) -> None:
        rows = [
            {
                "instanceId": "alpha__one-1",
                "resolved": True,
                "spendUsd": 0.05,
                "steps": 3,
                "decisionSteps": 0,
                "semanticSearchCalls": 2,
                "seconds": 10.0,
                "failure": None,
            }
        ]

        arm = summarize_arm("lunate", "budget", "opencode-go/budget", rows)
        arm["armId"] = "semantic-search-on"
        markdown = render_markdown(
            {
                "runId": "run-1",
                "dataset": "SWE-bench/SWE-bench_Verified",
                "split": "test",
                "total": 1,
                "maxCostUsd": 4.0,
                "totalSpendUsd": 0.05,
                "capReached": False,
                "arms": [arm],
            }
        )

        self.assertEqual(2, arm["semanticSearchCalls"])
        self.assertIn("Search calls", markdown)
        self.assertIn("| semantic-search-on |", markdown)
        self.assertIn("| 2 |", markdown)


class SwebenchThreeArmRunTests(unittest.TestCase):
    """The harness runs the two Lunate arms on one sample, each with its own configuration."""

    FAKE_ADAPTER = """\
import json
import sys
from pathlib import Path

args = sys.argv[1:]
def value(flag):
    return args[args.index(flag) + 1]

instance = value("--instance")
output = Path(value("--output-dir"))
run_dir = output / value("--run-id")
(run_dir / instance).mkdir(parents=True, exist_ok=True)
records = [
    {"type": "tool_call_start", "callId": "c1", "toolName": "bash"},
    {"type": "usage_updated", "modelId": "opencode-go/deepseek-v4.1-flash", "usage": {"inputTokenCount": 1000000, "outputTokenCount": 0}, "costUsd": 0.01},
]
if "--lunate-home" in args:
    settings = json.loads((Path(value("--lunate-home")) / "settings.json").read_text(encoding="utf-8"))
    embed = settings["profiles"]["embeddings"]["model"]
    records += [
        {"type": "tool_call_start", "callId": "c2", "toolName": "semantic_search"},
        {"type": "usage_updated", "modelId": embed, "usage": {"inputTokenCount": 1000, "outputTokenCount": 0}, "costUsd": 0.02},
    ]
(run_dir / instance / "trajectory.jsonl").write_text(
    "\\n".join(json.dumps(record) for record in records), encoding="utf-8"
)
(run_dir / "run.json").write_text(
    json.dumps({"instances": [{"instanceId": instance, "exitCode": 0, "hasPatch": True}]}),
    encoding="utf-8",
)
(run_dir / "official-results.json").write_text(
    json.dumps({"resolved_instances": 1, "unresolved_instances": 0, "dataset_instances": 1, "resolved_ids": [instance]}),
    encoding="utf-8",
)
"""

    def test_the_two_lunate_arms_run_on_the_same_instance(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "instances.txt").write_text("alpha__one-1\n", encoding="utf-8")
            (root / "run.json").write_text(
                json.dumps(
                    definition(
                        [
                            lunate_arm("semantic-search-off", "off"),
                            lunate_arm(
                                "semantic-search-on",
                                "on",
                                embeddings={
                                    "id": "google/gemini-embedding-001",
                                    "credential": "EMBED_KEY",
                                },
                            ),
                            baseline_arm(),
                        ]
                    )
                ),
                encoding="utf-8",
            )
            adapter = root / "fake_adapter.py"
            adapter.write_text(self.FAKE_ADAPTER, encoding="utf-8")
            binary = root / "lunate"
            binary.write_bytes(b"\x7fELF" + b"\x00" * 64)
            binary.chmod(0o755)

            result = subprocess.run(
                [
                    sys.executable,
                    str(REPOSITORY / "scripts" / "eval" / "swebench_run.py"),
                    "--config",
                    str(root / "run.json"),
                    "--work-dir",
                    str(root / "work"),
                    "--summary-dir",
                    str(root / "summary"),
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
                env={**os.environ, "CHAT_KEY": "placeholder", "EMBED_KEY": "placeholder"},
            )

            self.assertEqual(0, result.returncode, result.stderr)
            summary = json.loads(
                (root / "summary" / "run-1.lunate.json").read_text(encoding="utf-8")
            )
            off, on = summary["arms"]
            self.assertEqual("semantic-search-off", off["armId"])
            self.assertEqual("semantic-search-on", on["armId"])
            self.assertEqual("off", off["semanticSearch"])
            self.assertEqual("on", on["semanticSearch"])
            self.assertEqual("google/gemini-embedding-001", on["embeddingsModel"])
            self.assertEqual(1, off["resolved"])
            self.assertEqual(1, on["resolved"])
            self.assertEqual(0, off["semanticSearchCalls"])
            self.assertEqual(1, on["semanticSearchCalls"])
            self.assertEqual(0, off["instances"][0]["semanticSearchCalls"])
            self.assertEqual(1, on["instances"][0]["semanticSearchCalls"])
            # The off arm keeps its single model's lane price; the on arm adds the embedding record.
            self.assertAlmostEqual(0.15, off["spendUsd"])
            self.assertAlmostEqual(0.03, on["spendUsd"])
            self.assertTrue(
                (
                    root
                    / "work"
                    / "run-1"
                    / "lunate-home"
                    / "semantic-search-on"
                    / "settings.json"
                ).is_file()
            )
            self.assertFalse(
                (root / "work" / "run-1" / "lunate-home" / "semantic-search-off").exists()
            )
            self.assertTrue(
                (
                    root
                    / "work"
                    / "run-1"
                    / "lunate"
                    / "semantic-search-off-alpha__one-1"
                ).is_dir()
            )
            self.assertTrue(
                (
                    root
                    / "work"
                    / "run-1"
                    / "lunate"
                    / "semantic-search-on-alpha__one-1"
                ).is_dir()
            )


if __name__ == "__main__":
    unittest.main()
