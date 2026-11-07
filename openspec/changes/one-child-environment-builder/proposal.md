## Why

Review of the rc.3 branch (#621) found the detached tool child building its own environment in a second file (`ChildProcess.cs`), beside the allow-list builder the extensions capability already names as the one implementation. Two builders means two answers to "what does a child see", and the second one answered with everything — including the provider credentials Lunate itself resolved from the environment.

## What Changes

- **One builder, two named policies.** `EnvironmentScrubber.Build` takes a policy: `Scrubbed` (the minimal base set plus declared pass-through and declared values — the extension hosts) or `Inherited` (the caller's whole environment — the tool child). The inherited path moves out of `ChildProcess.cs` into the builder file.
- **No defaulted policy.** Every spawn site names `Scrubbed` or `Inherited` at its call; nothing answers by omission.
- **`Inherited` strips Lunate's own credentials.** A tool child inherits the caller's environment minus the provider credential variables Lunate resolves — the built-in presets' key variables and aliases, and every custom provider's `env:` credential reference. The user's own ambient variables (a toolchain's state, a shell's exports, `PATH`) still arrive: the bash tool is still the user's shell. **Behaviour change:** a model-chosen command can no longer read `OPENAI_API_KEY` and friends out of its environment, matching the guard's refusal of `auth.json` reads.
- **The guard tightens to one file and one rule.** `ChildEnvironmentGuardTests` pins `EnvironmentScrubber.cs` as the only environment builder and adds a test that no spawn site builds an environment itself — every child's environment comes out of the builder, whatever the policy.

## Capabilities

### New Capabilities

- `child-environment`: what a child process sees of the parent's environment — the two policies, the credential strip the inherited policy applies, and the single-builder rule every spawn site obeys.

### Modified Capabilities

None: the extensions capability's requirement already says the shared builder is the one implementation and Lunate's provider keys must not leak; this change makes the second policy and the tool child obey the same rule.

## Impact

- `Lunate.Extensibility.Abstractions`: `EnvironmentScrubber` gains `ChildEnvironmentPolicy` and the inherited policy; public API added to `PublicAPI.Unshipped.txt`.
- `Lunate.Coding`: `ChildProcess` delegates to the builder; `CredentialVariables` computes the strip list from the provider presets and the registered custom `env:` references (populated by `ProviderStore.Load`).
- `Lunate.Protocols`, `Lunate.Extensibility.Remote`, `Lunate.Extensibility.Testing`: call sites name `Scrubbed` (no behaviour change).
- Tests: `ChildEnvironmentGuardTests` (one builder, no site builds its own), new builder unit tests, and a POSIX end-to-end proof that a tool child sees the user's variables and not Lunate's keys.
