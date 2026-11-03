# Slice 2 of #474 — provider headers and credential placement

Refs #490 (design accepted, slice 1 merged as 09466aa3).

## What

This slice makes the declared provider headers and the credential actually go out on every wire
path: `ProviderHeadersHandler` (always a `lunate/<version>` user agent, then the provider's
declared headers with template substitution over `{session.id}`, `{run.id}`, `{lunate.version}`),
`ProviderCredentialHandler` (bearer default, `x-api-key`, a named header, or a query parameter;
removes any credential header the SDK added first so the key appears exactly once), the
credential-refusal forms (`{credential}`, `{apiKey}`, `{key}` are refused by name at load), and
`RequestContext(SessionId, RunId)` threaded through `IChatClientFactory.Create` with a per-run
ambient so a child session sends its own id.

## TDD ledger (in flight)

- [x] RED probe: `ProviderWireTests.A_plain_openai_chat_call_carries_the_lunate_user_agent_and_bears_the_credential`
      fails at `a_plain_openai_chat_call` — the wire today carries `User-Agent: OpenAI/2.14.0
      (...) MEAI/10.10.1` and the real key in SDK header form. (Symptom proven, not reproduced
      from stale memory: run against the merged slice 1.)
- [ ] GREEN after the handlers land.

# Slice 4 — Anthropic-compatible custom providers (#498)

Branch `wt/issue-498` off `origin/main` (4de288b9). TDD ledger for this slice:

- [x] Design read; the runtime path for a custom provider is `CustomProvider.ToModelInfo()` →
      catalog → `ChatClientFactory`; the shared writer is `SettingsStore.WriteProvider`, already
      used by both `auth login --custom` and the wizard, so "one writer" keeps holding by keeping
      both call sites on it.
- [ ] Unit A: `CustomProvider` carries `ApiFamily`/`CredentialPlacement`/`ModelsUrl`/`Headers`;
      unknown family refused by name; `ToModelInfo` routes the family's kind.
- [ ] Unit B: settings read/write the v2 entry shape (`baseUrls` keyed by family,
      `credentialPlacement`, optional `modelsUrl`, `headers`); legacy `baseUrl` still loads.
- [ ] Unit C: `auth login --custom --api-family/--credential-placement/--models-url` parse tests;
      omitted family defaults with a printed note.
- [ ] Unit D: bearer vs `x-api-key` round-trip settings → catalog → wire (capture transport).
- [ ] Unit E: discovery uses `modelsUrl` for a custom provider and refuses a cross-host redirect.
- [ ] Unit F: `ProviderStore` migration carries the family into the provider entry.

## Decisions taken

- **Omitted `--api-family` defaults to `openai-chat`, not `anthropic-messages`.** The issue text
  says "an omitted family defaults to Anthropic-compatible", but the approved design (design.md
  §7) pins `(default openai-chat)`, the parent issue #474 calls the family "a required answer"
  with today's custom path being OpenAI-compatible, and the wizard's own row is labelled
  "Custom OpenAI-compatible endpoint…". Defaulting to Anthropic would silently re-route every
  existing custom provider on re-login. The default is one constant; flipping it is a one-line
  change if the maintainer wants the issue's reading. Raised on the issue and in the PR.
