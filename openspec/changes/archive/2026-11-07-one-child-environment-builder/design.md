## Context

`EnvironmentScrubber.Build` (Lunate.Extensibility.Abstractions) builds the allow-list environment for the extension hosts: the extensions capability names it "the shared builder … one implementation". The rc.3 detachment work added a tool-child spawn that needed the opposite policy — the user's whole environment, because the bash tool is the user's shell — and built it in `ChildProcess.cs` instead of in the builder. The maintainer's ruling on that deviation: one builder file, two explicit policies, every spawn site naming one, and the inherited policy must strip Lunate's own credentials.

The credential names are known in two places: `ProviderPresets` (`keyVariable` and `keyVariableAlias` per built-in provider, loaded from `providers.json`) and the provider store's custom entries (`credentialReference` and header values may be `env:NAME`).

## Goals / Non-Goals

**Goals:**

- One environment builder file; both policies live in it; every call site names its policy.
- A tool child sees the user's environment but not a provider credential Lunate resolved.
- The guard test enforces both: one builder file, and no spawn site building an environment itself.

**Non-Goals:**

- No change to the extension hosts' policy or the SDK's `InheritEnvironmentVariables` handling.
- No filtering by value or pattern: the strip is an exact name list, so a variable that merely looks secret is the user's business.
- No new allow-list for the tool child: it stays inheritance-based, just minus Lunate's credentials.

## Decisions

- **Policy enum on the existing `Build`, no default.** `ChildEnvironmentPolicy { Scrubbed, Inherited }` is the first parameter; a call that omits it does not compile, which is what "the call site names its policy" means mechanically. Two entry points were the alternative; one method keeps a single place where the two policies are visible side by side.
- **`Inherited` strips only on the inherit path.** When a caller supplies `ProcessRunRequest.Environment`, that set replaces the environment by public contract (the scrubber's callers pass a fully-built set); the strip applies where inheritance actually happens, so a declared replacement is exactly what the caller declared. Declared values on top of an inherited set still drop a stripped name — the strip wins over the value, so a spawn site cannot accidentally re-add a credential.
- **The strip list is passed in, not looked up.** `EnvironmentScrubber` lives in `Lunate.Extensibility.Abstractions`, which references only `Lunate.Contracts`; the provider presets live in `Lunate.Ai`. `Lunate.Coding` (which references both) computes the names (`CredentialVariables`) and passes them, so the builder stays one implementation and the dependency direction stays intact.
- **Custom `env:` references are registered at `ProviderStore.Load`.** That is the one seam every path that resolves providers goes through, and it runs before any tool child can start. A name list that is a superset (all presets, not only the active one) is deliberate: Lunate resolving a key is the reason a model-chosen command must not see it, whichever provider the session happens to use.

## Risks / Trade-offs

- **A user script that reads `OPENAI_API_KEY` inside a bash command now gets nothing.** That is the point of the ruling, and the alternative — a model-chosen command reading the user's key — is the leak being closed. A user who wants the key in a command can export it inside the command from a file the guard already protects.
- **A registered custom `env:` name lingers for the process.** Harmless: the name list is only ever a deny-list, and a stale name strips nothing that is not there.
- **The guard now greps one file name instead of two.** A future spawn site must name a policy, which the new guard test fails on until it does — the review point is the same as before, only narrower.

## Migration Plan

None for users. Internal API change only; `PublicAPI.Unshipped.txt` gains the enum and the `Build` signature.

## Open Questions

None.
