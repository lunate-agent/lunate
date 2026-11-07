## 1. Provider preset and catalog data

- [x] 1.1 `providers.json`: declare the `systemone` base URL, the `systemone` `familyHeaders` image header and the `OPENCODE_GO_API_KEY` alias on the `opencode-go` preset (commit 91111cdd)
- [x] 1.2 `models.json`: pin the `opencode-go/jev-1.13` entry with `wireId: jev-1.13` and `apiFamily: systemone` (commit 91111cdd)

## 2. Setup wizard

- [x] 2.1 Label the `opencode-go` decision-profile line with the pay-as-you-go billing notice (no box questions), `opencode-go`-only (commit 91111cdd)

## 3. Tests

- [x] 3.1 Update preset/catalog family tests (`ChatClientFactoryFamilyTests`, `ModelCatalogFamilyTests`, `ModelCatalogTests`, `OpenCodeGoCatalogWireTests`, `ProviderPresetsTests`) (commit 91111cdd)
- [x] 3.2 Add wizard step-pin coverage for the Go Jev label (`SetupWizardStepPins`) (commit 91111cdd)
- [x] 3.3 Add key-gated live smoke `OpenCodeGoSystemOneLiveSmokeTests` in `Lunate.Coding.Tests` (decision request, pinned model; skipped without `LUNATE_LIVE=1` and `OPENCODE_GO_API_KEY`/`OPENCODE_API_KEY`) (commits 91111cdd, f5c29cd6)

## 4. Docs and gate

- [x] 4.1 Note the fifth family, the Go key alias and the pay-as-you-go billing in `docs/configuration.md` and `docs/guide.md` (commit 91111cdd)
- [x] 4.2 Run `openspec validate opencode-go-systemone --strict` and the narrow gate (Release build, focused test projects, CSharpier check) on the branch
