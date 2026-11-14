## Why

Every pull request runs the full three-OS matrix, and the merge queue then repeats macOS and Windows on the
same diff before it lands. The two shared-runner classes carry the flakiness and the capacity cost, while the
Linux legs already prove the change: a PR run and its `merge_group` run together spend ten verify legs (four
Ubuntu, four Windows, two macOS) where four Ubuntu legs answer the same question. Meanwhile the full matrix is
the one place a regression that only appears on Windows or macOS can still be caught, so it must keep running
on `main` and in the nightly.

## What Changes

- **`pull_request` — the fast gate.** `plan`, `changes`, `changelog`, the platform docs checks (`platform`) and
  build plus tests on `ubuntu-latest` only. No macOS, no Windows verify shards, no PowerShell twin, no
  dedicated perf leg.
- **`merge_group` — Ubuntu only.** The full test gate on `ubuntu-latest`, and none of the work the PR run
  already proved: `changes`, the changelog fragment guard, the platform/docs checks and the PowerShell twin are
  skipped. A prose-only batch runs `plan` only, because its PR already ran the docs profile.
- **Push to `main` and the nightly — the full matrix.** Ubuntu, Windows and macOS, every verify shard, plus
  `verify-powershell`, `perf` and the CodeQL workflow. This is the only place the three-OS gate runs.
- **The nightly reports itself.** A red `schedule` run files one `kind/bug` card per failing job in milestone
  `0.4.0`, naming the job and the run link, and comments on the open card for that job instead of filing a
  duplicate. The PowerShell-only reporter is replaced by this one, so one red night is one card per job.
- **The gate follows the event.** `ci-ok` remains the single required check and requires exactly the job set of
  the event it judges: the fast gate for a pull request, the Ubuntu test gate for a merge group, the full
  matrix for a push, the nightly and a manual dispatch. `ci-ok.sh --self-test` covers all of them, and the live
  pull-request decision requires the Ubuntu legs the plan now puts in a PR matrix.

The release gate is unchanged: `release.yml` and its tag contract are not touched.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the per-commit verification matrix becomes per event. A pull request runs the build and test
  gate on Ubuntu only; a merge group runs the Ubuntu test gate and skips the checks the PR proved; the full
  three-OS matrix, the PowerShell twin, the dedicated perf leg and CodeQL move to push-to-`main` and the
  nightly. The aggregate gate requires the job set of its event, and a red nightly files a deduplicated
  `kind/bug` card naming the failing job.

## Impact

- `.github/workflows/ci.yml`: the plan's leg decision, the per-event `if:` conditions of `changes`, `platform`,
  `verify-docs`, `verify-powershell` and `perf`, the new nightly reporter job, and the `ci-ok` environment.
- `.github/workflows/codeql.yml`: no change; CodeQL already runs on `main` and the weekly schedule.
- `scripts/ci-ok.sh`, `scripts/ci-ok-state`: the per-event decision table and the live pull-request verify
  matrix; the existing self-test is extended for the three events.
- `scripts/nightly-issue.sh` (new): the nightly reporter decision and the deduplicated filing, with a
  self-test that stubs `gh`.
- `tests/fixtures/ci-ok/**`: the live-head fixtures, where a pull request's own matrix is Ubuntu-only.
- `CONTRIBUTING.md` and `openspec/specs/ci-pipeline/spec.md`: the documented gate per event.
- No product code, public API, package or release-workflow change.
