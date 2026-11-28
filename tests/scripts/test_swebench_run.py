import json
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path

REPOSITORY = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPOSITORY / "scripts" / "eval"))

from swebench_run_support import (  # noqa: E402
    arm_chat_model,
    arm_credentials,
    arm_instance_spend,
    decision_home_files,
    decision_model_id,
    elapsed_seconds,
    instance_spend,
    load_definition,
    load_subset,
    official_counts,
    priced_spend,
    record_spend,
    render_markdown,
    summarize_arm,
    trajectory_totals,
)
import swebench_baseline  # noqa: E402
from swebench_baseline import baseline_command, run_baseline_instance  # noqa: E402
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

    def test_the_pinned_models_are_one_budget_one_frontier_and_the_decision_arm(self) -> None:
        definition = load_definition(EVAL / "run.json")

        tiers = sorted(model["tier"] for model in definition["models"])
        self.assertEqual(["budget", "decision", "frontier"], tiers)
        self.assertEqual("SWE-bench/SWE-bench_Verified", definition["dataset"])
        self.assertGreater(definition["limits"]["maxCostUsd"], 0)
        # The card's cap: one run stops at US$2.00 and reports what it got.
        self.assertEqual(2, definition["limits"]["maxCostUsd"])

    def test_the_decision_arm_is_the_card_configuration(self) -> None:
        definition = load_definition(EVAL / "run.json")
        decision = next(model for model in definition["models"] if model["tier"] == "decision")

        self.assertEqual("opencode-go/deepseek-v4.1-flash", arm_chat_model(decision))
        self.assertEqual("opencode-go/glm-5.3-flash", decision_model_id(decision))
        self.assertEqual("on", decision["decisionLayer"])
        self.assertEqual("OPENCODE_GO_API_KEY", decision["main"]["credential"])
        self.assertEqual("OPENCODE_GO_API_KEY", decision["decision"]["credential"])
        self.assertIn("decision layer", decision["note"])
        self.assertEqual(["OPENCODE_GO_API_KEY"], arm_credentials(decision))

    def test_a_second_decision_arm_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "run.json"
            decision = {
                "tier": "decision",
                "main": {"id": "opencode-go/main", "credential": "KEY"},
                "decision": {"id": "opencode-go/decision", "credential": "KEY"},
                "decisionLayer": "on",
                "note": "advises on routing and stops",
            }
            path.write_text(
                json.dumps(
                    {
                        "dataset": "SWE-bench/SWE-bench_Verified",
                        "split": "test",
                        "instancesFile": "instances.txt",
                        "limits": {"maxCostUsd": 2},
                        "models": [
                            {"tier": "budget", "id": "opencode-go/budget"},
                            {"tier": "frontier", "id": "opencode-go/frontier"},
                            decision,
                            decision,
                        ],
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaises(ValueError) as raised:
                load_definition(path)

            self.assertIn("at most one decision arm", str(raised.exception))

    def test_a_decision_arm_without_both_models_is_rejected(self) -> None:
        base = {
            "dataset": "SWE-bench/SWE-bench_Verified",
            "split": "test",
            "instancesFile": "instances.txt",
            "limits": {"maxCostUsd": 2},
        }
        single = [
            {"tier": "budget", "id": "opencode-go/budget"},
            {"tier": "frontier", "id": "opencode-go/frontier"},
        ]
        complete = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }
        flavours = {
            "'decision' model id": {**complete, "decision": {"credential": "KEY"}},
            "'main' model id": {k: v for k, v in complete.items() if k != "main"},
            "credential variable": {
                **complete,
                "decision": {"id": "opencode-go/decision"},
            },
            "'decisionLayer'": {**complete, "decisionLayer": "sometimes"},
            "note": {k: v for k, v in complete.items() if k != "note"},
        }
        for expected, arm in flavours.items():
            with self.subTest(expected=expected), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "run.json"
                path.write_text(
                    json.dumps({**base, "models": [*single, arm]}), encoding="utf-8"
                )

                with self.assertRaises(ValueError) as raised:
                    load_definition(path)

                self.assertIn(expected, str(raised.exception))

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


class SwebenchRunAdapterCommandTests(unittest.TestCase):
    def definition(self, home: str | None) -> dict:
        return {
            "_adapter": str(REPOSITORY / "scripts" / "eval" / "swebench.py"),
            "_lunate_bin": str(REPOSITORY / "artifacts" / "linux-x64" / "lunate"),
            "_lunate_home": home,
            "_eval_python": sys.executable,
            "_docker": "docker",
            "dataset": "SWE-bench/SWE-bench_Verified",
            "split": "test",
            "limits": {"maxTimeSeconds": 1800},
            "instanceTimeoutSeconds": 1800,
        }

    def test_a_lunate_home_reaches_the_adapter_and_the_off_arm_omits_it(self) -> None:
        model = {"tier": "budget", "id": "example/model", "credential": "OPENROUTER_API_KEY"}

        with_home = swebench_run.adapter_command(
            self.definition("/tmp/prepared-home"), "alpha__one-1", model, 5.0, Path("/tmp/out")
        )
        without_home = swebench_run.adapter_command(
            self.definition(None), "alpha__one-1", model, 5.0, Path("/tmp/out")
        )

        self.assertEqual("/tmp/prepared-home", with_home[with_home.index("--lunate-home") + 1])
        self.assertNotIn("--lunate-home", without_home)
        self.assertEqual(
            [part for part in without_home if part != "--lunate-home"],
            [part for part in with_home if part not in ("--lunate-home", "/tmp/prepared-home")],
        )

    def test_the_decision_arm_passes_the_main_model_home_and_credential(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "FAKE_KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "FAKE_KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }

        with mock.patch.dict(__import__("os").environ, {"FAKE_KEY": "placeholder"}):
            command = swebench_run.adapter_command(
                self.definition(None),
                "alpha__one-1",
                model,
                2.0,
                Path("/tmp/out"),
                "home/decision",
            )

        self.assertEqual("opencode-go/main", command[command.index("--model") + 1])
        self.assertEqual("home/decision", command[command.index("--lunate-home") + 1])
        forwarded = [command[index + 1] for index, part in enumerate(command) if part == "--env"]
        self.assertIn("FAKE_KEY", forwarded)
        self.assertEqual(1, forwarded.count("FAKE_KEY"), "the shared credential is forwarded once")

    def test_the_decision_home_binds_the_decision_profile(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }

        home = decision_home_files(model)
        settings = home["settings.json"]
        providers = home["providers.json"]["providers"]

        self.assertEqual("opencode-go/main", settings["profiles"]["main"]["model"])
        self.assertEqual("opencode-go/decision", settings["profiles"]["decision"]["model"])
        self.assertEqual("main", settings["usages"]["chat"])
        self.assertEqual("decision", settings["decisions"]["profile"])
        self.assertEqual("shadow", settings["decisions"]["routing"])
        self.assertEqual(["main", "decision"], settings["decisions"]["routingProfiles"])
        self.assertEqual("shadow", settings["decisions"]["approvals"])
        # The note says the decision model advises on routing and stops, so the home turns on
        # both consumers — the approval gate included — and neither acts (all shadow).
        self.assertNotIn("enforce", settings["decisions"].values())
        # A profile resolves only against a configured provider, so the home names one per preset
        # and resolves the credential from the environment, never from the file.
        self.assertEqual(1, len(providers))
        entry = providers[0]
        self.assertEqual(settings["profiles"]["main"]["provider"], entry["name"])
        self.assertEqual(settings["profiles"]["decision"]["provider"], entry["name"])
        self.assertEqual("opencode-go", entry["kind"])
        self.assertEqual("env:KEY", entry["credentialReference"])
        self.assertEqual(
            "https://opencode.ai/inference/go/openai/v1", entry["baseUrls"]["openai-chat"]
        )
        self.assertIn("x-opencode-session", entry["headers"])
        self.assertNotIn("KEY", json.dumps(home).replace("env:KEY", ""))

    def test_a_model_on_an_unknown_provider_is_refused(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "nowhere/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }

        with self.assertRaises(ValueError) as raised:
            decision_home_files(model)

        self.assertIn("nowhere/decision", str(raised.exception))

    def test_a_layer_off_arm_writes_no_decisions_block(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "off",
            "note": "the layer stays off",
        }

        settings = decision_home_files(model)["settings.json"]

        self.assertNotIn("decisions", settings)
        self.assertEqual("opencode-go/decision", settings["profiles"]["decision"]["model"])

    def test_the_decision_home_is_written_lf_and_readable(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }
        with tempfile.TemporaryDirectory() as directory:
            home = swebench_run.write_decision_home(Path(directory), model)

            written = (home / "settings.json").read_bytes()
            providers = (home / "providers.json").read_bytes()
            self.assertNotIn(b"\r\n", written)
            self.assertNotIn(b"\r\n", providers)
            self.assertNotIn(b"\xef\xbb\xbf", written)
            self.assertEqual("decision", json.loads(written)["decisions"]["profile"])
            self.assertEqual("shadow", json.loads(written)["decisions"]["approvals"])
            self.assertEqual(
                "env:KEY", json.loads(providers)["providers"][0]["credentialReference"]
            )

    def test_only_a_decision_arm_carries_a_prepared_home(self) -> None:
        with self.assertRaises(ValueError):
            decision_home_files({"tier": "budget", "id": "opencode-go/budget"})


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

    def test_a_decision_call_is_not_an_agent_step(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trajectory.jsonl"
            lines = [
                {
                    "type": "usage_updated",
                    "modelId": "opencode-go/main",
                    "usage": {"inputTokenCount": 10, "outputTokenCount": 2},
                    "costUsd": 0.25,
                }
                for _ in range(3)
            ] + [
                {
                    "type": "usage_updated",
                    "modelId": "opencode-go/decision",
                    "usage": {"inputTokenCount": 5, "outputTokenCount": 1},
                    "costUsd": 0.01,
                }
            ]
            path.write_text("\n".join(json.dumps(line) for line in lines), encoding="utf-8")

            totals = trajectory_totals(path, "opencode-go/decision")

            self.assertEqual(3, totals["steps"])
            self.assertEqual(1, totals["decisionSteps"])
            self.assertEqual(4, totals["usageRecords"])

    def test_a_multi_model_arm_prices_every_record(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trajectory.jsonl"
            path.write_text(
                "\n".join(
                    json.dumps(line)
                    for line in (
                        {
                            "type": "usage_updated",
                            "modelId": "opencode-go/main",
                            "usage": {"inputTokenCount": 2_000_000, "outputTokenCount": 0},
                            "costUsd": 0.3,
                        },
                        {
                            "type": "usage_updated",
                            "modelId": "opencode-go/decision",
                            "usage": {"inputTokenCount": 1_000_000, "outputTokenCount": 0},
                            "costUsd": 0.02,
                        },
                    )
                ),
                encoding="utf-8",
            )

            totals = trajectory_totals(path, "opencode-go/decision")

            # The two records are priced at their own rates, not both at the main model's.
            self.assertAlmostEqual(0.32, arm_instance_spend(model, totals, {"input": 0.15}))
            self.assertAlmostEqual(0.32, record_spend(totals))

    def test_a_record_without_a_price_makes_the_arm_unpriced(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "trajectory.jsonl"
            path.write_text(
                json.dumps(
                    {
                        "type": "usage_updated",
                        "modelId": "opencode-go/main",
                        "usage": {"inputTokenCount": 1000, "outputTokenCount": 10},
                    }
                ),
                encoding="utf-8",
            )

            totals = trajectory_totals(path, "opencode-go/decision")

            self.assertIsNone(arm_instance_spend(model, totals, {"input": 0.15}))

    def test_a_single_model_arm_keeps_the_lane_price(self) -> None:
        model = {"tier": "budget", "id": "opencode-go/budget", "credential": "KEY"}
        totals = {
            "inputTokens": 1_000_000,
            "outputTokens": 0,
            "cacheReadTokens": 0,
            "cacheWriteTokens": 0,
            "costUsd": 0.0,
            "recordCostUsd": 99.0,
            "uncostedRecords": 0,
            "steps": 1,
            "limit": None,
        }

        self.assertAlmostEqual(0.15, arm_instance_spend(model, totals, {"input": 0.15}))

    def test_a_cost_limit_event_wins_over_both_price_paths(self) -> None:
        model = {
            "tier": "decision",
            "main": {"id": "opencode-go/main", "credential": "KEY"},
            "decision": {"id": "opencode-go/decision", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }
        totals = {
            "uncostedRecords": 2,
            "recordCostUsd": 0.0,
            "limit": {"kind": "cost", "maxCostUsd": 2, "spendUsd": 2.01},
        }

        self.assertAlmostEqual(2.01, arm_instance_spend(model, totals, None))

    def test_elapsed_seconds_reads_the_run_stamps(self) -> None:
        self.assertEqual(90.0, elapsed_seconds("2026-11-14T11:25:15Z", "2026-11-14T11:26:45Z"))
        self.assertIsNone(elapsed_seconds(None, "2026-11-14T11:26:45Z"))
        self.assertIsNone(elapsed_seconds("yesterday", "2026-11-14T11:26:45Z"))


class SwebenchRunBaselineSpendTests(unittest.TestCase):
    def baseline_definition(self) -> dict:
        return {
            "dataset": "SWE-bench/SWE-bench_Verified",
            "split": "test",
            "instanceTimeoutSeconds": 60,
            "_docker": "docker",
            "_eval_python": sys.executable,
            "_mini_extra": "mini-extra",
            "baseline": {
                "models": {"opencode-go/deepseek-v4.1-flash": "openai/deepseek-v4.1-flash"},
                "stepLimit": 50,
                "costLimitUsd": 3,
            },
        }

    def run_baseline_with_cost(self, cost: object) -> dict:
        import subprocess
        from unittest import mock

        model = {"tier": "budget", "id": "opencode-go/deepseek-v4.1-flash"}
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory) / "work"
            trajectory = work / "baseline" / "budget-alpha__one-1.traj.json"
            trajectory.parent.mkdir(parents=True)
            trajectory.write_text(
                json.dumps(
                    {
                        "info": {
                            "model_stats": {"instance_cost": cost, "api_calls": 3},
                            "exit_status": "Submitted",
                            "submission": "diff --git a/x b/x\n",
                        }
                    }
                ),
                encoding="utf-8",
            )
            completed = subprocess.CompletedProcess(args=[], returncode=0, stdout="", stderr="")
            with mock.patch.object(
                swebench_baseline, "run_command", return_value=completed
            ), mock.patch.object(
                swebench_baseline,
                "baseline_counts",
                return_value={
                    "resolved": 1,
                    "resolvedIds": ["alpha__one-1"],
                },
            ):
                return run_baseline_instance(
                    self.baseline_definition(), "alpha__one-1", model, work, 10.0
                )

    def test_a_zero_baseline_cost_is_unpriced_not_zero(self) -> None:
        row = self.run_baseline_with_cost(0)

        self.assertIsNone(row["spendUsd"])
        self.assertTrue(row["resolved"])

    def test_a_missing_baseline_cost_is_unpriced_not_zero(self) -> None:
        row = self.run_baseline_with_cost(None)

        self.assertIsNone(row["spendUsd"])
        self.assertTrue(row["resolved"])

    def test_a_positive_baseline_cost_is_reported(self) -> None:
        row = self.run_baseline_with_cost(0.0423)

        self.assertAlmostEqual(0.0423, row["spendUsd"])


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

    def test_the_arm_reports_wall_time_and_cost_per_resolve(self) -> None:
        rows = [
            {
                "instanceId": "alpha__one-1",
                "resolved": True,
                "spendUsd": 0.25,
                "steps": 4,
                "decisionSteps": 2,
                "seconds": 30.0,
                "failure": None,
            },
            {
                "instanceId": "beta__two-2",
                "resolved": False,
                "spendUsd": 0.35,
                "steps": 6,
                "decisionSteps": 3,
                "seconds": 60.0,
                "failure": "unresolved",
            },
        ]

        arm = summarize_arm("lunate", "decision", "opencode-go/main", rows)

        self.assertEqual(90.0, arm["seconds"])
        self.assertEqual(10, arm["steps"])
        self.assertEqual(5, arm["decisionSteps"])
        self.assertAlmostEqual(0.6, arm["costPerResolvedUsd"])

    def test_an_arm_without_a_resolve_has_no_cost_per_resolve(self) -> None:
        rows = [
            {
                "instanceId": "alpha__one-1",
                "resolved": False,
                "spendUsd": 0.5,
                "steps": 4,
                "seconds": 10.0,
                "failure": "unresolved",
            }
        ]

        arm = summarize_arm("lunate", "budget", "opencode-go/budget", rows)

        self.assertEqual(0, arm["resolved"])
        self.assertIsNone(arm["costPerResolvedUsd"])

    def test_an_unpriced_arm_has_no_cost_per_resolve(self) -> None:
        rows = [
            {
                "instanceId": "alpha__one-1",
                "resolved": True,
                "spendUsd": None,
                "steps": 4,
                "seconds": 10.0,
                "failure": None,
            }
        ]

        arm = summarize_arm("lunate", "budget", "opencode-go/budget", rows)

        self.assertIsNone(arm["spendUsd"])
        self.assertIsNone(arm["costPerResolvedUsd"])

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

    def test_the_markdown_shows_wall_time_and_cost_per_resolve(self) -> None:
        summary = {
            "runId": "run-1",
            "dataset": "SWE-bench/SWE-bench_Verified",
            "split": "test",
            "total": 1,
            "maxCostUsd": 2.0,
            "totalSpendUsd": 0.4,
            "capReached": False,
            "arms": [
                {
                    "arm": "lunate",
                    "tier": "decision",
                    "model": "opencode-go/deepseek-v4.1-flash",
                    "decisionModel": "opencode-go/glm-5.3-flash",
                    "resolved": 1,
                    "total": 1,
                    "spendUsd": 0.4,
                    "steps": 12,
                    "seconds": 245.5,
                    "costPerResolvedUsd": 0.4,
                    "failures": [],
                }
            ],
        }

        markdown = render_markdown(summary)

        self.assertIn("Wall time", markdown)
        self.assertIn("$/resolved", markdown)
        self.assertIn("245.5s", markdown)
        self.assertIn("$0.4000", markdown)
        self.assertIn("| 12 |", markdown)

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

    def test_the_decision_arm_is_a_third_lunate_arm_with_its_own_home(self) -> None:
        import subprocess

        fake_adapter = """\
import json
import sys
from pathlib import Path

args = sys.argv[1:]
def value(flag):
    return args[args.index(flag) + 1]

instance = value("--instance")
model = value("--model")
output = Path(value("--output-dir"))
run_dir = output / value("--run-id")
(run_dir / instance).mkdir(parents=True, exist_ok=True)
records = [{
    "type": "usage_updated",
    "modelId": model,
    "usage": {"inputTokenCount": 1000, "outputTokenCount": 10},
    "costUsd": 0.01,
}]
if "--lunate-home" in args:
    home = Path(value("--lunate-home"))
    settings = json.loads((home / "settings.json").read_text(encoding="utf-8"))
    records.append({
        "type": "usage_updated",
        "modelId": settings["profiles"]["decision"]["model"],
        "usage": {"inputTokenCount": 100, "outputTokenCount": 5},
        "costUsd": 0.02,
    })
(run_dir / instance / "trajectory.jsonl").write_text(
    "\\n".join(json.dumps(record) for record in records), encoding="utf-8"
)
(run_dir / "run.json").write_text(
    json.dumps({
        "instances": [{
            "instanceId": instance,
            "exitCode": 0,
            "hasPatch": True,
            "startedAt": "2026-11-14T11:25:15Z",
            "finishedAt": "2026-11-14T11:26:15Z",
        }],
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
        decision_main = "opencode-go/deepseek-v4.1-flash"
        decision_model = "opencode-go/glm-5.3-flash"
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "instances.txt").write_text("alpha__one-1\n", encoding="utf-8")
            config = root / "run.json"
            config.write_text(
                json.dumps(
                    {
                        "dataset": "SWE-bench/SWE-bench_Verified",
                        "split": "test",
                        "instancesFile": "instances.txt",
                        "limits": {"maxCostUsd": 2, "maxTimeSeconds": 60},
                        "instanceTimeoutSeconds": 60,
                        "models": [
                            {"tier": "budget", "id": "opencode-go/budget", "credential": "FAKE_KEY"},
                            {
                                "tier": "frontier",
                                "id": "openrouter/anthropic/claude-opus-5.5",
                                "credential": "FAKE_KEY",
                            },
                            {
                                "tier": "decision",
                                "main": {"id": decision_main, "credential": "FAKE_KEY"},
                                "decision": {"id": decision_model, "credential": "FAKE_KEY"},
                                "decisionLayer": "on",
                                "note": "advises on routing and stops",
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
            self.assertEqual(["budget", "frontier", "decision"], [item["tier"] for item in arm["arms"]])
            decision_row = arm["arms"][2]
            self.assertEqual("lunate", decision_row["arm"])
            self.assertEqual(decision_main, decision_row["model"])
            self.assertEqual(decision_model, decision_row["decisionModel"])
            self.assertEqual("on", decision_row["decisionLayer"])
            self.assertEqual(1, decision_row["resolved"])
            self.assertEqual(1, decision_row["steps"])
            self.assertEqual(1, decision_row["decisionSteps"])
            self.assertAlmostEqual(0.03, decision_row["spendUsd"])
            self.assertEqual(60.0, decision_row["instances"][0]["seconds"])
            self.assertAlmostEqual(0.03, decision_row["costPerResolvedUsd"])
            # The configuration travels with the numbers: the generated home is in the run tree.
            home = json.loads(
                (work / "run-1" / "lunate-home" / "decision" / "settings.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual("decision", home["decisions"]["profile"])
            self.assertEqual("shadow", home["decisions"]["approvals"])
            self.assertEqual(decision_model, home["profiles"]["decision"]["model"])


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

    def test_the_baseline_arm_keeps_completed_lunate_artifacts(self) -> None:
        from unittest import mock

        def runner(definition, instance_id, model, work_root, remaining):
            return {
                "instanceId": instance_id,
                "resolved": False,
                "spendUsd": 0.5,
                "failure": None,
            }

        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            lunate = work / "lunate" / "budget-alpha__one-1"
            lunate.mkdir(parents=True)
            (lunate / "run.json").write_text("{}", encoding="utf-8")
            with mock.patch.object(swebench_run, "run_baseline_instance", side_effect=runner):
                swebench_run.run_arm(self.definition(), ["alpha__one-1"], "baseline", work)

            self.assertTrue(lunate.is_dir())
            self.assertTrue((lunate / "run.json").is_file())

    def test_an_unpriced_baseline_instance_stops_the_baseline_arm(self) -> None:
        from unittest import mock

        calls: list[tuple[str, str]] = []

        def runner(definition, instance_id, model, work_root, remaining):
            calls.append((instance_id, model["tier"]))
            return {
                "instanceId": instance_id,
                "resolved": False,
                "spendUsd": None,
                "failure": None,
            }

        with tempfile.TemporaryDirectory() as directory:
            with mock.patch.object(swebench_run, "run_baseline_instance", side_effect=runner):
                arm = swebench_run.run_arm(
                    self.definition(), ["alpha__one-1", "beta__two-2"], "baseline", Path(directory)
                )

        self.assertEqual(
            {("alpha__one-1", "budget"), ("alpha__one-1", "frontier")}, set(calls)
        )
        self.assertIsNone(arm["totalSpendUsd"])
        self.assertTrue(arm["capReached"])

    def test_the_cap_stops_before_the_next_instance(self) -> None:
        from unittest import mock

        calls: list[str] = []

        def runner(definition, instance_id, model, work_root, remaining):
            calls.append(instance_id)
            return {
                "instanceId": instance_id,
                "resolved": False,
                "spendUsd": remaining,
                "failure": None,
            }

        with tempfile.TemporaryDirectory() as directory:
            with mock.patch.object(swebench_run, "run_lunate_instance", side_effect=runner):
                arm = swebench_run.run_arm(
                    self.definition(cap=15.0),
                    ["alpha__one-1", "beta__two-2"],
                    "lunate",
                    Path(directory),
                )

        self.assertEqual(["alpha__one-1", "alpha__one-1"], calls)
        self.assertTrue(arm["capReached"])
        self.assertAlmostEqual(15.0, arm["totalSpendUsd"])
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


    def test_baseline_is_not_started_when_the_lunate_total_is_unknown(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "instances.txt").write_text("alpha__one-1\n", encoding="utf-8")
            (root / "run.json").write_text(
                json.dumps(
                    {
                        "dataset": "SWE-bench/SWE-bench_Verified",
                        "split": "test",
                        "instancesFile": "instances.txt",
                        "limits": {"maxCostUsd": 10, "maxTimeSeconds": 60},
                        "models": [
                            {"tier": "budget", "id": "opencode-go/budget"},
                            {"tier": "frontier", "id": "opencode-go/frontier"},
                        ],
                    }
                ),
                encoding="utf-8",
            )
            (root / "run-1.lunate.json").write_text(
                json.dumps({"totalSpendUsd": None}), encoding="utf-8"
            )

            result = swebench_run.main(
                [
                    "--config",
                    str(root / "run.json"),
                    "--work-dir",
                    str(root / "work"),
                    "--summary-dir",
                    str(root),
                    "--run-id",
                    "run-1",
                    "--arm",
                    "baseline",
                    "--mini-extra",
                    "missing-mini-extra",
                ]
            )

            self.assertEqual(0, result)
            self.assertFalse((root / "run-1.baseline.json").exists())

    def test_the_baseline_arm_skips_the_decision_tier(self) -> None:
        from unittest import mock

        tiers: list[str] = []

        def runner(definition, instance_id, model, work_root, remaining):
            tiers.append(model["tier"])
            return {
                "instanceId": instance_id,
                "resolved": True,
                "spendUsd": 0.1,
                "steps": 3,
                "seconds": 12.0,
                "failure": None,
            }

        definition = self.definition()
        definition["models"] = definition["models"] + [
            {
                "tier": "decision",
                "main": {"id": "opencode-go/main", "credential": "KEY"},
                "decision": {"id": "opencode-go/decision", "credential": "KEY"},
                "decisionLayer": "on",
                "note": "advises on routing and stops",
            }
        ]

        with tempfile.TemporaryDirectory() as directory:
            with mock.patch.object(swebench_run, "run_baseline_instance", side_effect=runner):
                arm = swebench_run.run_arm(
                    definition, ["alpha__one-1"], "baseline", Path(directory)
                )

        self.assertEqual({"budget", "frontier"}, set(tiers))
        self.assertEqual(["budget", "frontier"], [item["tier"] for item in arm["arms"]])


class SwebenchRunRowTests(unittest.TestCase):
    """One instance row carries the four numbers the card compares."""

    def run_dir(self, root: Path) -> Path:
        run_dir = root / "lunate" / "decision-alpha__one-1"
        (run_dir / "alpha__one-1").mkdir(parents=True)
        (run_dir / "alpha__one-1" / "trajectory.jsonl").write_text(
            "\n".join(
                json.dumps(line)
                for line in (
                    {
                        "type": "usage_updated",
                        "modelId": "opencode-go/main",
                        "usage": {"inputTokenCount": 1000, "outputTokenCount": 100},
                        "costUsd": 0.01,
                    },
                    {
                        "type": "usage_updated",
                        "modelId": "opencode-go/main",
                        "usage": {"inputTokenCount": 2000, "outputTokenCount": 200},
                        "costUsd": 0.02,
                    },
                    {
                        "type": "usage_updated",
                        "modelId": "opencode-go/glm-5.3-flash",
                        "usage": {"inputTokenCount": 500, "outputTokenCount": 50},
                        "costUsd": 0.005,
                    },
                )
            ),
            encoding="utf-8",
        )
        (run_dir / "run.json").write_text(
            json.dumps(
                {
                    "instances": [
                        {
                            "instanceId": "alpha__one-1",
                            "exitCode": 0,
                            "hasPatch": True,
                            "startedAt": "2026-11-14T11:25:15Z",
                            "finishedAt": "2026-11-14T11:28:20Z",
                        }
                    ]
                }
            ),
            encoding="utf-8",
        )
        (run_dir / "official-results.json").write_text(
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
        return run_dir

    def decision_arm(self) -> dict:
        return {
            "tier": "decision",
            "main": {"id": "opencode-go/deepseek-v4.1-flash", "credential": "KEY"},
            "decision": {"id": "opencode-go/glm-5.3-flash", "credential": "KEY"},
            "decisionLayer": "on",
            "note": "advises on routing and stops",
        }

    def test_the_row_records_resolved_spend_steps_and_wall_time(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            run_dir = self.run_dir(Path(directory))

            row = swebench_run.read_lunate_row(
                {}, "alpha__one-1", self.decision_arm(), run_dir, 0
            )

        self.assertTrue(row["resolved"])
        self.assertAlmostEqual(0.035, row["spendUsd"])
        self.assertEqual(2, row["steps"])
        self.assertEqual(1, row["decisionSteps"])
        self.assertEqual(185.0, row["seconds"])
        self.assertIsNone(row["failure"])

    def test_a_row_without_run_stamps_has_no_wall_time(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            run_dir = self.run_dir(Path(directory))
            (run_dir / "run.json").write_text(
                json.dumps({"instances": [{"instanceId": "alpha__one-1", "exitCode": 0}]}),
                encoding="utf-8",
            )

            row = swebench_run.read_lunate_row(
                {}, "alpha__one-1", self.decision_arm(), run_dir, 0
            )

        self.assertIsNone(row["seconds"])


class SwebenchRunEvidenceTests(unittest.TestCase):
    """The committed summary must point at evidence a clean checkout can read."""

    def test_the_default_work_dir_is_the_committed_runs_tree(self) -> None:
        arguments = swebench_run.parse_args([])

        self.assertEqual(EVAL / "runs", Path(arguments.work_dir))

    def test_every_instance_row_links_committed_official_evidence(self) -> None:
        summary = json.loads((EVAL / "summary.json").read_text(encoding="utf-8"))

        for arm in summary["arms"]:
            for row in arm["instances"]:
                directory = Path(row["workDir"])
                self.assertFalse(
                    directory.is_absolute(), f"{row['instanceId']}: workDir is a machine-local path"
                )
                self.assertEqual(
                    ("eval", "swebench", "runs"),
                    directory.parts[:3],
                    f"{row['instanceId']}: workDir is outside the committed run tree",
                )
                predictions_path = REPOSITORY / directory / "predictions.jsonl"
                self.assertTrue(
                    predictions_path.is_file(), f"missing committed prediction: {predictions_path}"
                )
                predictions = [
                    json.loads(line)
                    for line in predictions_path.read_text(encoding="utf-8").splitlines()
                    if line.strip()
                ]
                self.assertEqual(1, len(predictions), f"{row['instanceId']}: not one prediction")
                self.assertEqual(row["instanceId"], predictions[0]["instance_id"])
                self.assertEqual(arm["model"], predictions[0]["model_name_or_path"])
                self.assertEqual(
                    row["hasPatch"],
                    bool(predictions[0]["model_patch"].strip()),
                    f"{row['instanceId']}: the committed prediction disagrees with the row",
                )
                report_path = REPOSITORY / directory / "report.json"
                if not report_path.is_file():
                    # The official harness was not asked to evaluate an empty patch, so an
                    # instance without one has no report; the committed prediction is its record.
                    self.assertFalse(row["hasPatch"], f"{row['instanceId']}: patched row has no report")
                    self.assertFalse(row["resolved"], f"{row['instanceId']}: unresolved row has no report")
                    continue
                report = json.loads(report_path.read_text(encoding="utf-8"))[row["instanceId"]]
                self.assertEqual(
                    row["resolved"],
                    report["resolved"],
                    f"{row['instanceId']}: the committed official report disagrees with the row",
                )
                self.assertEqual(
                    row["hasPatch"],
                    report["patch_exists"],
                    f"{row['instanceId']}: the committed submission record disagrees with the row",
                )


if __name__ == "__main__":
    unittest.main()
