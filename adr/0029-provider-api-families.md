# ADR-0029: A provider is a credential plus one base URL per API family

Status: proposed — 2026-11-02
Supersedes: the single-base-URL half of ADR-0027 (the provider store itself is unchanged)

## Context

The provider store was built around one provider = one kind = one base URL = one wire adapter. That
held while every provider offered one API. It no longer does. OpenCode Go serves 28 models across four
native APIs — OpenAI Chat Completions, OpenAI Responses, Anthropic Messages and Google `generateContent`
— behind three base URLs, and its discovery endpoint returns each model's API family and base URL as
metadata. A provider model that cannot say "this model speaks Responses, that one speaks Messages" must
either send every model through one adapter (wrong wire format, wrong features) or grow a per-provider
fork (the thing the layering rules exist to prevent).

Two defects surfaced while checking the contract against the docs: provider `headers` are parsed and
then never applied to a request, and no client sets `User-Agent`, although OpenCode Go's console asks
clients to identify themselves and to send a stable per-conversation session header.

## Decision

1. A provider entry holds its credential and **a base URL per API family** it offers
   (`openai-chat`, `openai-responses`, `anthropic-messages`, `google`; the set is extensible in code,
   not by configuration typo), plus an optional models/discovery URL.
2. **The model carries the family.** Resolution: profile override, then discovery metadata, then the
   catalog entry, then the provider's only family. More than one family and no answer is an error.
   The family selects the adapter; the provider kind no longer does.
3. **Headers are part of the provider contract**: static values plus templates over a fixed variable
   set `{session.id}`, `{run.id}`, `{lunate.version}`, validated at load, applied by one HTTP handler
   on every provider path including discovery. `User-Agent: lunate/<version>` is always sent.
4. Providers remain **user scope only**. A project can reference a provider; it cannot define one, its
   URLs or its headers. A custom provider's discovered base URLs must share its configured host.
5. The OpenAI Responses family uses the SDK client already referenced by
   `Microsoft.Extensions.AI.OpenAI` 10.10.1; the Google family needs a small native client. No new
   package is introduced.

## Consequences

- Existing provider files keep working: `baseUrl` is the shorthand for the default family.
- OpenCode Go, and any provider with per-family endpoints, becomes expressible without a fork.
- The session header needs a session id at request time; a call without one fails loudly rather than
  sending an empty value.
- Every adapter shares one header pipeline, so a new adapter cannot silently omit identification or
  a provider's headers.
- The Google client is the one large piece (~600–900 lines with fixtures); it is the change's risk
  and is called out in the design for an explicit accept/cut decision.

## Alternatives rejected

- **Keep one base URL and let models declare absolute endpoints.** Works for OpenCode Go, but loses
  the family: the adapter is still chosen by kind, so a Responses model would go through the Chat
  Completions path. It also makes every model entry carry a URL that discovery already knows.
- **One provider entry per family** (`opencode-go-openai`, `opencode-go-anthropic`, …). Puts four
  entries and four credentials in front of the user for one subscription, and the wizard would have to
  teach the split.
- **A per-provider request builder.** The layering rules and the ADR-0010 native-adapter decision both
  point the other way.
- **Send headers in each adapter.** That is how the current dead `Headers` field happened.
