## 1. Fixtures and failing tests first

- [ ] 1.1 Add `tests/fixtures/discovery/opencode-go-multi-api-models.json`: one multi-API entry recommending a supported family, one recommending the live `interactions` family, one without a recommendation, one recommending a family it does not declare, and one ordinary entry.
- [ ] 1.2 Add `tests/fixtures/discovery/undecipherable-models.json` whose invalid `base_url` echoes a fake credential in the parser's message.
- [ ] 1.3 Update the Gemini entry in `tests/fixtures/discovery/opencode-go-live-models.json` to the current live shape (`generate-content` plus `interactions`, `recommended_api: interactions`) and adjust the live-listing expectation.
- [ ] 1.4 Add `DiscoverCommandTests` cases proving a supported recommendation is taken, an unusable multi-API entry is skipped without failing the listing, and the unusable-listing fixture still fails discovery with its parser message.
- [ ] 1.5 Add `WizardDiscoveryTests` cases proving the unreadable-listing fixture classifies as `Undecipherable`, its parser message redacts the typed credential, and a transport failure still classifies as `Network`.
- [ ] 1.6 Add a `SetupWizardModelStepTests` case proving the connection-test notice shows the underlying parser message with `[redacted]` replacing the credential.
- [ ] 1.7 Run the focused test classes and confirm the new cases fail before the implementation.

## 2. Implementation

- [ ] 2.1 Give `DiscoveryModelMetadataReader` a read/skip/invalid outcome; resolve several API families through the model-level (or nested) `recommended_api` only when it names a supported declared family, skipping the entry otherwise.
- [ ] 2.2 Let `DiscoverCommand.ParseListing` skip such an entry and continue.
- [ ] 2.3 Classify listing-body failures as `Undecipherable`, keeping transport failures as `Network` and HTTP/auth statuses as they are.
- [ ] 2.4 Redact the resolved credential in `WizardDiscovery.RunAsync`'s failure result and print the underlying message in the wizard notice.
- [ ] 2.5 Run the focused test classes again until green.

## 3. Documentation and release note

- [ ] 3.1 Add the release-note fragment `changes/<pr>.md` under `### Fixed`.
- [ ] 3.2 Tick this task list, run `dotnet csharpier format .`, the focused Release builds/tests and `openspec validate --all --strict`.

## 4. Finish

- [ ] 4.1 Archive as the last commit: `openspec archive setup-discovery-multi-api --yes`.
