# 0009 (addendum) — The idle gate follows the shipped ReadyToRun subject on .NET 11

- Status: accepted — 2026-11-16 (re-calibration under ADR-0009's median + 50% rule, measured by #902)
- Date: 2026-11-16
- Relates to: ADR-0009 (memory budgets), ADR-0008 (release publish flags), ADR-0040 (.NET 11 targets), #902
- Amends: ADR-0009

## Context

ADR-0009 set the idle gate at 150 MB from an 82 MB single-file + ReadyToRun baseline (S-5), before the
shipped interactive stack existed. #902 re-measured the shipped subject on .NET 11 with the ADR-0009
method: peak RSS of the whole process tree, sampled every 150 ms from outside the process over a 20 s
idle window. The sandbox denied a pty and `ps`, so the measurement used the product's own
`IConsoleIO` seam and libproc's `proc_pid_rusage` counter; the substitutions are recorded in the
change design.

The results separate the session from the bundle:

- Framework-dependent host: 88 MB on .NET 11 (85 MB on .NET 10).
- Single-file + ReadyToRun (the shipped shape): 353 MB on .NET 11 (298 MB on .NET 10), with bundle
  sizes of 439 MiB and 377 MiB.
- Single-file without ReadyToRun: 85 MB on .NET 11.

The shipped ReadyToRun subject is over the 150 MB gate on both runtimes. The excess is the mapped
ReadyToRun images, not the session, and ADR-0009 calibrated the gate on that same single-file +
ReadyToRun shape.

## Decision

1. **The idle gate is recalibrated to 550 MB.** ADR-0009's rule is median + ~50%, rounded up to the
   next 50 MB: 353 × 1.5 = 529.5, and the next 50 is 550. `scripts/memory.sh` now defaults
   `MEMORY_GATE_MB` to 550.
2. **The target stays 100 MB.** It is the session-footprint goal, reported rather than enforced; the
   framework-dependent and non-ReadyToRun rows show the session stays near it.
3. **The per-OS CI values stay provisional.** `memory_gate_mb` remains 150 until the
   post-merge/nightly runners measure their own subject; the CI memory step has no `MEMORY_SUBJECT`
   configured, so those values gate nothing today. Re-calibrating them from this machine's macOS
   arm64 measurement would be a guess, not per-runner evidence.
4. **The release shape does not change.** ADR-0008 keeps single-file + ReadyToRun; dropping it to
   meet the old gate would change the release architecture, not the retarget.

## Alternatives considered

- **Keep 150 MB and treat the 88 MB framework-dependent row as the budget-relevant result.**
  Rejected: ADR-0009 calibrated the idle gate on the single-file + ReadyToRun subject, and the
  shipped artifact is that shape. The framework-dependent row describes a different packaging.
- **Make the shipped subject meet 150 MB by disabling or compressing ReadyToRun.** Rejected: this
  contradicts ADR-0008 and is a release-architecture decision outside #902.
- **Set the per-OS CI values from the macOS measurement.** Rejected: RSS accounting and bundle
  mapping differ per OS, and only the post-merge/nightly runners can supply their own evidence.

## Consequences

- The idle gate now guards the shipped ReadyToRun bundle mapping: a bundle regression of about 50%
  still trips it. A session-only regression remains visible in the framework-dependent/non-R2R rows
  and the 100 MB target.
- The first post-merge/nightly memory run with a real subject re-checks the per-runner values; until
  then the CI matrix values are placeholders, not measurements.
- The startup budget is not amended: the .NET 11 floor measured 131–148 ms, inside the local 150 ms
  default (ADR-0005 addendum).
