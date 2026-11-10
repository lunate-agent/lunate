## Context

`lunate /auth list` reports bounded reachability for every provider entry by sending one
unauthenticated `HEAD` request to the entry's base URL
(`src/Lunate.Coding/ProviderReachabilityProbe.cs`). It builds that request by hand instead of going
through the provider transport's handler chain
(`ProviderHeadersHandler`, which every real provider path uses). ADR-0029 decision 3 and the
`ai-layer` "Provider request headers" requirement make `User-Agent: lunate/<version>` a property of
every provider request, including discovery; the probe was the last path outside that handler chain
and therefore the last one sending no agent string (review finding N-7, #587).

The probe is deliberately unauthenticated: it sends no credential and no provider-declared header,
because a reachability check must not leak a key to an endpoint that is merely being probed. Only the
agent string is needed.

## Goals / Non-Goals

**Goals:**

- The probe request carries `User-Agent: lunate/<informational version>`, from the same
  `RequestContext.LunateVersion` value the eight provider transports render, so there is one version
  source on the wire.
- The probe still sends no credential, no provider header, no body, and keeps its bounded timeout and
  its three-valued answer (`Reachable`, `Unreachable`, `Unknown`).

**Non-Goals:**

- Not routing the probe through `ProviderHeadersHandler`. That handler also writes
  provider-declared headers, including credential templates; a probe that resolved them would need a
  session context and could place a key on an unverified endpoint.
- Not touching discovery or chat transports: they already carry the header (tests
  `DiscoverCommandTests`, `ProviderWireTests`, `OpenAI/Google/OpenCodeGo` wire tests).

## Decisions

**Set the header on the probe's request, not on the shared `HttpClient`.** `AuthCommand.List` creates
one `HttpClient` for all rows, with `DefaultRequestHeaders` empty; setting the agent there would also
work but would couple attribution to the caller that happens to construct the client, and the probe is
the type a future caller composes. The probe writes it itself:

- remove any existing `User-Agent` (matching `ProviderHeadersHandler`'s "exactly one agent, ours"
  rule), then `TryAddWithoutValidation("User-Agent", RequestContext.LunateVersion)`;
- do it on the request it already creates, so the timeout, status handling and error mapping are
  untouched.

Alternative considered: a dedicated `DelegatingHandler` for the probe. Rejected — one header on one
request does not justify a second handler chain, and the probe is the only caller.

Alternative considered: reading the version a second time from the assembly in `Lunate.Coding`.
Rejected — `RequestContext.LunateVersion` is already `internal` and visible to `Lunate.Coding`
(`Lunate.Ai` exposes internals to `Lunate.Coding`), so a second implementation would be a second
truth for "what version does this binary say it is".

## Risks / Trade-offs

- [A provider that rejects unknown agents now sees an extra header on the probe] -> The header is the
  product's own, the same one every chat request already sends to that provider; the probe's answer
  is derived from transport success/failure, not from status code.
- [The probe running outside any run context] -> `RequestContext.LunateVersion` is a static
  assembly-derived value, not the ambient per-run context; it never needs a session and so cannot fail
  with an unresolvable template.

## Migration Plan

None. No stored data, configuration, session file or public API changes.

## Open Questions

None. ADR-0029 already requires the header on every provider path; this change brings the probe into
line with it, so no ADR is created or superseded.
