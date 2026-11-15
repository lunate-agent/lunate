# Tasks — design-hook-extensibility

## 1. The decision documents

- [x] 1.1 `proposal.md`: why the observing surface is missing while the intercepting catalogue and the
      `AgentEvent` stream already exist, with links to the code that proves each claim; no behaviour
      change, so no spec delta (`skip_specs`).
- [x] 1.2 Compare Lunate's hook model with Pi's `pi.on()` extension events and Claude Code's hooks:
      handler shape, declaration, ordering, failure, timeout, interception, observation and
      out-of-process reach — from the upstream documentation, not from memory.
- [x] 1.3 `design.md` with the component and sequence views (C4 diagram rule): the tap on the harness
      emit path, the per-subscriber bounded queue and pump, the two mechanisms side by side, and the
      `--json`/ACP projections of the same records.
- [x] 1.4 Answer the #897 open questions in the design: BCL `IObservable` only (with ADR-0007's Rx
      confinement), in-process/out-of-process parity as catalogue-now plus declared event projection on
      protocol 3, the relation to `--json`, and the relation to the existing extension contract.

## 2. The durable record

- [x] 2.1 Read every ADR in `adr/`, build the supersession graph and confirm no in-force ADR decides the
      observing surface (manifest in `adr.md`).
- [x] 2.2 Draft `adr/0040-hook-based-extensibility-on-observables.md` as **proposed — awaiting
      maintainer sign-off**, with the decisions, consequences and rejected alternatives; do not edit any
      prior ADR.

## 3. Close

- [ ] 3.1 Add the release-note fragment `changes/<pr>.md` under the release heading (the lane supplies
      the pull-request number).
- [ ] 3.2 Run `dotnet csharpier check .`, markdownlint on the new files and
      `openspec validate design-hook-extensibility --type change --strict`; then
      `openspec validate --all --strict`.
- [ ] 3.3 Archive the change with `openspec archive design-hook-extensibility --yes` as the last commit,
      after the maintainer's ADR sign-off is requested in the pull request (an architecture decision is
      proposed and stopped for, never decided by the worker).
