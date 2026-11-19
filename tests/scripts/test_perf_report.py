"""Pins the startup budget gate naming its measurement when it fails.

A red `perf (macos-latest)` on `main` is triaged from the run's check run, not from its log: the job
page of a public run answers "Must have admin rights" to anyone without a session, while GitHub keeps
every `::error::` line of the step in the check run's annotations, where
`gh api .../check-runs/<id>/annotations` returns it anonymously. `Startup budget (dedicated
measurement)` of run 44466042157 failed with the annotation "Process completed with exit code 1" and
nothing else: `scripts/perf.sh` printed its floor, median and budget into the log alone, so the card
could not tell the shared macOS runner's known 300-330 ms noise from a doubled startup (#950).

The gate's own numbers are what the annotation now carries, beside the job summary that renders on the
run page: the failing floor, the median, the budget and, when the noise policy retried, the first
attempt's floor, which is the value a re-calibration reads (ADR-0005). A failure on a leg that prints
the measurement without failing on it (`STARTUP_BUDGET_ENFORCE=0` in ci.yml's verify matrix) annotates
nothing — a breach and an unreadable measurement alike — because that leg is green and the annotation
would accuse it.

`hyperfine` and the binary are stubs, so the tests need neither a published build nor a measurement.
"""

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
PERF = REPOSITORY_ROOT / "scripts" / "perf.sh"

# The shim answers `--version`, then copies the measurement a test staged into the `--export-json` path
# hyperfine was asked for. `{payload}` is the file `timing()` writes; the second body lets the noise
# policy's retry read a different measurement than the first attempt.
HYPERFINE_SHIM = """#!/usr/bin/env bash
if [ "$1" = "--version" ]; then
  echo "hyperfine 2.0.0"
  exit 0
fi
out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --export-json)
      out="$2"
      shift 2
      ;;
    *) shift ;;
  esac
done
{measurement}
"""

MEASURE_ONCE = 'cat "{payload}" >"$out"\nexit 0'
# The counter stages the noise policy: the first measurement breaches, the retry clears it.
MEASURE_TWICE = """if [ -f "{counter}" ]; then
  cat "{retry}" >"$out"
else
  : >"{counter}"
  cat "{first}" >"$out"
fi
exit 0"""


def timing(floor: float, median: float) -> str:
    """One measurement in the shape hyperfine 2.x writes, with floor and median in seconds."""
    return json.dumps(
        {
            "results": [
                {
                    "command": "x",
                    "summary": {
                        "time_wall_clock": {
                            "unit": "second",
                            "count": 3,
                            "mean": 0.011,
                            "stddev": 0.001,
                            "median": median,
                            "min": floor,
                            "max": 0.013,
                        }
                    },
                }
            ]
        }
    )


class PerfReportTests(unittest.TestCase):
    def setUp(self) -> None:
        self.directory = Path(tempfile.mkdtemp(prefix="lunate-perf-report-"))
        self.addCleanup(lambda: subprocess.run(["rm", "-rf", str(self.directory)], check=False))
        binary = self.directory / "lunate"
        binary.write_text("#!/usr/bin/env bash\nexit 0\n", encoding="utf-8")
        binary.chmod(0o755)
        self.binary = binary
        self.shim = self.directory / "bin"
        self.shim.mkdir()
        self.summary = self.directory / "summary.md"

    def stage(self, name: str, payload: str) -> Path:
        path = self.directory / name
        path.write_text(payload, encoding="utf-8")
        return path

    def hyperfine(self, measurement: str) -> None:
        path = self.shim / "hyperfine"
        path.write_text(HYPERFINE_SHIM.format(measurement=measurement), encoding="utf-8")
        path.chmod(0o755)

    def run_perf(self, budget: str, summary: bool = False, enforce: str = "1") -> subprocess.CompletedProcess:
        environment = {
            key: value
            for key, value in os.environ.items()
            if key not in ("GITHUB_STEP_SUMMARY", "STARTUP_BUDGET_ENFORCE")
        }
        environment.update(
            {
                "PATH": f"{self.shim}:{environment['PATH']}",
                "BUDGET_MS": budget,
                "PERF_RUNS": "2",
                "PERF_WARMUP": "0",
                "PERF_RESULTS": str(self.directory / "perf.json"),
                "STARTUP_BUDGET_ENFORCE": enforce,
            }
        )
        if summary:
            environment["GITHUB_STEP_SUMMARY"] = str(self.summary)
        return subprocess.run(
            ["bash", str(PERF), str(self.binary)],
            capture_output=True,
            text=True,
            env=environment,
            check=False,
        )

    def test_a_breach_names_its_numbers_in_an_error_annotation(self) -> None:
        """The annotation is the one place a reader without an account sees the measurement."""
        self.hyperfine(MEASURE_ONCE.format(payload=self.stage("breach.json", timing(0.010, 0.012))))

        result = self.run_perf("5")

        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn(
            "::error::perf: startup budget exceeded: fastest of 2 runs 10 ms > 5 ms "
            "(median 12 ms; first attempt 10 ms)",
            result.stdout,
        )

    def test_the_run_summary_carries_the_breach_too(self) -> None:
        """The job summary renders on the public run page, so it carries the same sentence."""
        self.hyperfine(MEASURE_ONCE.format(payload=self.stage("breach.json", timing(0.010, 0.012))))

        result = self.run_perf("5", summary=True)
        written = self.summary.read_text(encoding="utf-8")

        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("fastest of 2 runs 10 ms > 5 ms", written)

    def test_a_breach_the_retry_clears_annotates_nothing(self) -> None:
        """The noise policy's second measurement is the verdict; a cleared breach is not a red run."""
        self.hyperfine(
            MEASURE_TWICE.format(
                counter=self.directory / "measured",
                first=self.stage("first.json", timing(0.010, 0.012)),
                retry=self.stage("retry.json", timing(0.001, 0.002)),
            )
        )

        result = self.run_perf("5")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("::error::", result.stdout)

    def test_a_measuring_but_not_enforcing_leg_annotates_nothing(self) -> None:
        """`STARTUP_BUDGET_ENFORCE=0` keeps the step green, so an error annotation would accuse it."""
        self.hyperfine(MEASURE_ONCE.format(payload=self.stage("breach.json", timing(0.010, 0.012))))

        result = self.run_perf("5", enforce="0")

        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("startup budget exceeded", result.stderr)
        self.assertNotIn("::error::", result.stdout)

    def test_an_unreadable_measurement_annotates_nothing_on_a_non_enforcing_leg(self) -> None:
        """The broken measurement fails `perf.sh`, but `verify.sh` keeps that leg green: no annotation."""
        self.hyperfine(
            MEASURE_ONCE.format(
                payload=self.stage("unreadable.json", '{"results": [{"command": "x"}]}')
            )
        )

        result = self.run_perf("5", enforce="0")

        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("could not read the startup measurement", result.stderr)
        self.assertNotIn("::error::", result.stdout)

    def test_an_unreadable_measurement_is_annotated(self) -> None:
        """A gate that cannot read its own number fails, and says so where the check run shows it."""
        self.hyperfine(
            MEASURE_ONCE.format(
                payload=self.stage("unreadable.json", '{"results": [{"command": "x"}]}')
            )
        )

        result = self.run_perf("5")

        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn("::error::perf: could not read the startup measurement", result.stdout)


if __name__ == "__main__":
    unittest.main()
