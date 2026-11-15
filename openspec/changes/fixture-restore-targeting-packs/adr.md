# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change. No decision in `design.md` establishes a long-term architectural
commitment: the change configures how the gate's *test* restores resolve the packs the pinned SDK asks
for, it touches no product boundary, contract, dependency or pattern, and the durable statement it does
make — a fixture restored by an SDK that does not bundle the product's targeting packs resolves them
offline from the machine's NuGet cache — is recorded as a requirement of the `ci-pipeline` spec, where
the rest of the fixture-restore contract already lives.

## In-Force ADRs Reviewed

- `adr/0001-no-aot-single-file-budgets.md` — the SDK pin decides the managed runtime of the published
  binary; this change leaves the pin and the publishing flags untouched.
- `adr/0011-repo-quality-tooling.md` — the tooling the gate runs (CSharpier, the test runners); this
  change adds no tool and no dependency.
- `adr/0037-ci-gate-sized-by-event.md` — which legs run on which event; this change explains why a
  macOS/Windows-only regression can pass a pull request that runs Ubuntu only, and does not change the
  matrix.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
