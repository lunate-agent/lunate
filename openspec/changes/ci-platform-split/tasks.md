# Tasks

## 1. Split the gate

- [x] `scripts/verify.sh`: `platform_checks()` and the `platform` profile; `VERIFY_SKIP_PLATFORM_CHECKS=1`
      makes an OS leg skip the platform-independent steps while a local run keeps them.
- [x] `scripts/verify.sh`: the packaging-file check moved into `platform_checks` (it is source-only).
- [x] `.github/workflows/ci.yml`: `platform` job (restore, openspec, `verify.sh --profile platform`);
      `ci-ok` requires it; OS legs set `VERIFY_SKIP_PLATFORM_CHECKS=1`.
- [x] `scripts/verify.ps1` honors `VERIFY_SKIP_PLATFORM_CHECKS=1`: the PowerShell leg skips
      formatting, docs lint and the public-API diff, which run once in `platform` (#435); a local
      Windows run keeps the full gate.

## 2. Path-filter the PowerShell leg

- [x] `scripts/ci-powershell-paths.sh`: deny-by-default decision, Windows markers derived from file
      content, `--self-test` with nine cases.
- [x] `.github/workflows/ci.yml`: `changes` job produces `powershell=true|false`; the PowerShell leg
      needs it and is gated on it.
- [x] Triggers: `main` pushes, tags `v*`, nightly schedule, manual dispatch force the leg on.
- [x] Nightly failure files a deduplicated `kind/bug` issue on the 0.3 milestone.

## 3. The aggregate gate

- [x] `scripts/ci-ok.sh`: `PLATFORM` and `PS_REQUIRED` inputs; skipped-required and unreadable
      states are red; self-test grew from 31 to 44 checks and pins the new wiring.
- [x] `scripts/ci-ok.sh --self-test` green, including the four PowerShell states.

## 4. Prove it

- [x] `verify.sh --profile platform` green on the Mac Mini (the new profile, run for real): 24/24 openspec items, all checks passed.
- [ ] The pull request's own CI green with the new topology.
- [ ] Measure five consecutive pull requests after the merge; post the table on #435.
- [ ] Decide between balanced shards and publish-once-per-OS from those numbers (deferred, not this
      change).

## 5. Spec

- [x] `openspec/changes/ci-platform-split/specs/ci-pipeline/spec.md`: matrix and aggregate-gate
      requirements updated.
- [ ] Archive this change once the PR is merged.
