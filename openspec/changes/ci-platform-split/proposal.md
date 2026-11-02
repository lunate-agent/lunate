# CI: one platform job, and a PowerShell leg that only runs when it can break

## Problem

The gate re-proves the same platform-independent facts on every runner, and it runs a 22-minute
Windows-only leg on every pull request whatever the change touches.

Measured on run 41433140264 (`main`, 2026-11-02): the run's wall was 22.1 min and `verify-powershell`
was 21.8 of them — the critical path, but only 4.3 min ahead of the next leg. Inside that leg the
tests took 9.6 min, publish 4.3, format 2.6. The same format step, the same docs-sync, the same
public-API diff and the same OpenSpec validation ran in *every* "shard 1" leg: three OSes plus the
PowerShell leg. Formatting identical source on four runners is cost, not depth.

The PowerShell leg exists because Windows-only behaviour has broken before; that risk is real but it
is not uniform across changes. A pull request that touches a Markdown page, a Linux-only file or the
extension contract cannot move it, and paying 22 minutes of Windows time for it is what made the
gate the bottleneck (#435).

## What changes

- **One `platform` job on Linux** runs the checks that do not depend on the OS — CSharpier +
  `dotnet format`, the generated docs regions, markdownlint, the public API, `openspec validate`, the
  scratch-path and spec-archive checks, and the packaging-file assertion. The OS legs run
  `verify.sh` with `VERIFY_SKIP_PLATFORM_CHECKS=1` and keep build, test, publish and the artifact
  proofs, which genuinely differ per machine. `ci-ok` requires the platform job.
- **The PowerShell leg is path-filtered**, decided by `scripts/ci-powershell-paths.sh` (self-tested,
  deny-by-default): it runs when the change touches a PowerShell script, `install.ps1`, packaging or
  workflow files, or any source file whose content carries a Windows-specific marker
  (`OperatingSystem.IsWindows`, `OSPlatform.Windows`, `SupportedOSPlatform("windows")`,
  `#if WINDOWS`, a Windows RID). It always runs on `main` pushes, tags, the nightly schedule and
  manual dispatch. The Windows bash leg is untouched and runs on every pull request.
- **`ci-ok` learns the difference between a legitimate skip and a missing leg**: PowerShell skipped
  with `changes.powershell == 'false'` is a pass; skipped or cancelled while `'true'` is red; an
  unreadable decision is red.
- **A red nightly files its own issue** (kind/bug, 0.3), deduplicated, so a regression on `main`
  cannot sit unnoticed until the next tag.

## Impact

- Affected specs: `ci-pipeline` — the matrix requirement and the aggregate-gate requirement.
- Affected code: `.github/workflows/ci.yml`, `scripts/verify.sh`, `scripts/ci-ok.sh`,
  `scripts/ci-powershell-paths.sh` (new).
- Expected effect, to be verified on five consecutive pull requests: wall time ~22 → ~15 min
  (format once), plus the PowerShell leg off unrelated changes.
- No product code, no user-visible surface, no new dependency.
