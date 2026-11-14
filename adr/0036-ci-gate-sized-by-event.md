# ADR-0036: The CI gate is sized by the event, and the full matrix lives on main and the nightly

Status: accepted — 2026-11-14

## Context

Every pull request ran the full three-OS verification matrix, and the merge queue repeated macOS and Windows on
the same diff before the merge. Ten verify shards ran twice per change, and the two shared-runner classes with
the flakiest timings and the tightest capacity were the expensive half of both runs. The three-OS gate is still
the only way to see an OS-specific regression, and the single required `ci-ok` check must name every leg, or a
matrix can shrink without anyone noticing.

The maintainer direction in #831 fixes the split: a pull request gets a fast gate on Ubuntu, a merge group gets
the Ubuntu test gate without re-running what the pull request proved, and the full matrix plus the PowerShell
twin, the dedicated perf leg and CodeQL belongs to a push to `main` and the nightly.

## Decision

1. **The event decides the matrix.** `plan` emits Windows and macOS verify legs only for a push to `main`, the
   nightly and a manual dispatch. A pull request and a merge group run Ubuntu only.
2. **A pull request runs the checks that cannot be postponed** — plan, the changed-path decision, the changelog
   guard, the platform/docs checks and the Ubuntu build and test shards — and no Windows, macOS, PowerShell or
   perf leg.
3. **A merge group runs the Ubuntu test gate and skips what the pull request proved** — the changed-path
   decision, the changelog guard, the platform/docs checks, the docs profile and the PowerShell twin. A
   prose-only batch runs the plan only.
4. **The full matrix, the PowerShell twin, the perf measurement and CodeQL run on a push to `main` and the
   nightly.** The perf result feeds the nightly's verdict, not a push's.
5. **`ci-ok` requires exactly its event's job set** and fails when a job that event does not require ran; its
   self-test evaluates the pull request, merge group and push/nightly decisions and pins the plan's pull-request
   matrix to the live decision.
6. **A red nightly reports itself**: one `kind/bug` card per failing job in milestone `0.4.0`, naming the job and
   the run link, reused while an open card for that job exists.

## Consequences

- A change proves itself once before the queue; the queue's wall time is bounded by the Ubuntu shards instead of
  the slowest macOS or Windows leg.
- Windows- and macOS-only regressions are caught on `main` and the nightly rather than on the pull request. The
  nightly files a card for the failing job, so the later detection is visible instead of silent.
- The gate's shape is now a per-event table in two places (`ci.yml` and `scripts/ci-ok.sh`); the self-test is the
  contract that keeps them the same, and every new job must be added to both.
- A prose-only merge group has no test to run. That is intentional: merging prose cannot break a build, and the
  pull request already ran the docs profile.
- The release gate is untouched; tags still belong to the Release workflow.
