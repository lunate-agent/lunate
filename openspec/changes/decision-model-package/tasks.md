## 1. OpenSpec and records

- [ ] 1.1 Write the proposal, delta specs, design and ADR manifest; add ADR-0045 amending ADR-0044.
- [ ] 1.2 Validate the change with `openspec validate decision-model-package --strict`.

## 2. The model package

- [ ] 2.1 Create `src/Lunate.Decisions.Model/Lunate.Decisions.Model.csproj` as a packable project
      versioned with the contract pair, with its README, icon and repository metadata.
- [ ] 2.2 Move the model records and the two seams into it, keeping the `Lunate.Ai.Decisions`
      namespace; move `DecisionOptions` and the usage bookkeeping to the engine.
- [ ] 2.3 Add `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` for the model package.

## 3. References and packaging

- [ ] 3.1 Make `Lunate.Ai.Decisions` reference the model, keep the engine unpacked, and keep the
      engine types internal.
- [ ] 3.2 Make `Lunate.Agent` reference the model as a published dependency and keep packing only
      `Lunate.Ai.dll` and `Lunate.Ai.Decisions.dll`.
- [ ] 3.3 Make `Lunate.Extensibility.Abstractions` reference the model package and delete its
      `PackOwnAssemblies` target; update its README.
- [ ] 3.4 Point `Lunate.Extensibility`, `Lunate.Extensibility.Compile` and `HostOwnedAssemblies` at
      the model assembly; embed the model in the compile contract.
- [ ] 3.5 Add the model package to `scripts/pack-dry-run.sh`, `.github/workflows/release.yml`,
      `pack.yml`'s documented list and the verify contract-package roundtrip.

## 4. Tests

- [ ] 4.1 Update the assembly-identity, contract-package inventory, layering and compile-contract
      tests for the new assembly name and the model package.
- [ ] 4.2 Add a test that the model types and both seams live in `Lunate.Decisions.Model`, that the
      engine types stay internal, and that the pack list and release workflow name the package.
- [ ] 4.3 Run the named test classes and a Release build of every touched project.

## 5. Docs and close-out

- [ ] 5.1 Update `docs/dev/architecture.md`, `AGENTS.md`, the contract and package READMEs and the
      extension authoring pages that name the host-owned decision assembly.
- [ ] 5.2 Write `changes/<pull-request-number>.md`.
- [ ] 5.3 Format with CSharpier, run the fast gate, then sync specs and archive the change as the
      last commit.
