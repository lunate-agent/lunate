# 0008 — Release builds: single file + ReadyToRun, no single-file compression

- Status: accepted — 2026-10-04 (maintainer sign-off)
- Date: 2026-10-04
- Spike: `docs/spikes/S-5/report.md` (crash evidence under `docs/spikes/S-5/evidence/startup-crashes.txt`)
- Relates to: ADR 0001 (single-file releases), ADR 0005 (startup budgets)

## Context

Release builds so far used `PublishSingleFile` + `PublishReadyToRun` +
`EnableCompressionInSingleFile`. Spike S-5 measured the exact release flag
combination and found:

1. **Crashes.** With the exact flags, the VariantBPlus binary died with
   `AccessViolationException` in 11/100 starts and VariantA in 1/100; baseline
   and VariantB were stable (0/100). Without compression the same binaries were
   stable (B+ 0/150), as were framework-dependent builds (0/200) and
   single-file-without-R2R builds (0/150). Root cause not isolated (runtime /
   packaging interaction on macOS 26, arm64, .NET 10.0.103).
2. **Startup cost.** Compression adds ~80 ms to startup: baseline 103 ms
   (compressed) vs 24 ms (uncompressed), measured with hyperfine on the same
   machine. The uncompressed delta sits well inside the local budget (ADR 0005).

Download size is unaffected in practice: release archives (`.tar.gz` / `.zip`)
compress the binary at the distribution layer, so users still download a
~30 MB archive; only the on-disk binary is larger (~105 MB).

## Decision

- **Release builds are single file + ReadyToRun without
  `EnableCompressionInSingleFile`.** `scripts/verify.sh` and the release
  workflow publish uncompressed; release archives stay compressed.
- This supersedes the "compression on" wording of ADR 0001's release row.
- **Follow-up results (2026-10-04):** the crash probe on CI runners
  (`win-x64`, `linux-x64`; 100 starts × 4 variants × 2 flag sets each) found
  **0 crashes** — the crash is macOS-specific so far. On macOS the independent
  re-run reproduced it (VariantBPlus 13/100 compressed) while minimal
  timer-only apps without Lunate code did **not** (0/600). No `dotnet/runtime`
  issue is drafted; deeper isolation stays open. Evidence:
  `docs/spikes/S-5/evidence/crash-probe-ci.txt` and `crash-probe-local.txt`.
- **Follow-up (2026-10-09):** the macOS crash reproduces with a third,
  unrelated dependency stack — XenoAtom.Terminal.UI, 3/300 compressed
  single-file R2R starts (spike S-6; evidence:
  `docs/spikes/S-6/evidence/startup-crashes.txt`). Stacks that share only the
  runtime/thread-pool path exhibit it, strengthening the runtime hypothesis;
  isolation stays open.
- **Follow-up (2026-11-22, re-measured for #654):** the decision stands, now
  with numbers beside it (`docs/dev/spikes/S-9/report.md`). On `osx-arm64`
  the shipped shape is a 440.6 MiB binary and a 139.0 MB archive at a 139–177
  ms startup floor; compression gives a 137.2 MiB binary and a 134.2 MB
  archive at 860–910 ms, i.e. 3.5% of the download for about 720 ms on every
  launch, so the flag stays out — for the startup cost, not only for the
  crash, which did not reproduce in 100 starts on .NET 11. The same
  measurement shows ReadyToRun is the size (+318.5 MB of the binary, 2.7× the
  download) *and*, on this RID, a startup cost of ~100 ms — the opposite of
  the disk-for-startup trade this ADR assumed. Turning it off is left open
  deliberately: it revises this record and needs the `linux-x64` and `win-x64`
  startup floors from CI before anyone decides it.

## Alternatives considered

- **Keep compression on** and root-cause the crash first: rejected — the crash
  is a release risk for an unknown-until-isolated duration, and compression
  costs ~80 ms on every start of a CLI meant to feel instant.
- **Framework-dependent releases**: rejected — "no runtime to install" is a
  product goal (ADR 0001).
- **Compress only some targets**: rejected — inconsistent artifacts for an
  unexplained crash; uniformity is worth more than the on-disk size.

## Consequences

- `scripts/verify.sh` and `.github/workflows/release.yml` no longer pass
  `EnableCompressionInSingleFile`.
- The verify gate now exercises the release flag set without compression; a
  future re-introduction of compression must cite new evidence.
- The crash probe and minimal-repro results are recorded in `docs/spikes/S-5/`
  and the S-5 report (macOS-only and non-minimal so far); deeper isolation is
  open follow-up work.
