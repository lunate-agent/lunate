# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0001 — no AOT, single-file budgets: the release shape is untouched; the archives stay
  self-contained single files, so a user installs no runtime.
- ADR-0002 — layering and MEAI model types: no project reference, package reference or model type
  changes; only the target framework and the language version.
- ADR-0005 — startup budgets, and its addendum (the gate reads the floor): the move re-measures the
  published binary against a `net10.0` baseline built from `origin/main` and leaves the calibrated
  budgets alone, because the floor did not move materially.
- ADR-0008 — release build shape: `PublishSingleFile` + ReadyToRun without compression is unchanged.
- ADR-0017 — extensibility: its contract packages are exactly the ones that keep their `net10.0`
  asset; the `ExtensionApi` version and the public API are untouched.
- ADR-0037 — CI gate sized by event: the gate keeps its shape; the OS legs build one more asset per
  contract package, no new job.
- ADR-0039 — `Mermaid.Terminal` as a standalone, Lunate-free package: the package stays standalone and
  keeps a `net10.0` asset for the same reason it is standalone — a host that knows nothing about the
  agent.

No superseded ADR constrains this change.

## New Durable ADRs Created

- `adr/0040-target-frameworks.md` — the tool, the app and every first-party project target `net11.0`
  with `LangVersion 15.0`; the extension-facing packages multi-target `net10.0;net11.0` with one
  public API; the `dotnet tool` package is the `net11.0` asset alone and the release note says so; the
  startup budget is re-measured rather than assumed.

## Open Questions

None. The two breaking options — dropping `net10.0` from the contract packages, and a `net10.0` tool
asset — are decided against in ADR-0040, per #192's decision, and stay separate decisions if they are
ever wanted.
