## Why

The `ai-layer` spec requires every provider request to identify the client with
`User-Agent: lunate/<informational version>`, but the bounded reachability probe behind
`lunate /auth list` sends a bare `HEAD` with no `User-Agent` (#587, review finding N-7). A provider
endpoint therefore sees a request from an unnamed client — the one provider-facing path that escapes
the attribution rule.

## What Changes

- The reachability probe writes `User-Agent: lunate/<version>` on its request, from the same
  `RequestContext.LunateVersion` value every provider transport already uses — no second copy of the
  version string.
- The probe's request is then indistinguishable in attribution from a chat request; it still sends
  no credential and no provider-declared headers (it is an unauthenticated reachability check).
- The `ai-layer` "Provider request headers" requirement names the reachability probe among the
  requests that carry the header, so the spec and the code agree.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `ai-layer`: the "Provider request headers" requirement enumerates the reachability probe as a
  request that carries `User-Agent: lunate/<version>`; its "Every request identifies Lunate" scenario
  covers the probe.

## Impact

- `src/Lunate.Coding/ProviderReachabilityProbe.cs` — the request gains the user agent.
- `tests/Lunate.Coding.Tests/ProviderReachabilityTests.cs` — the production-path test asserts the
  header on every probe request.
- No public API change, no new package, no session-format change. The probe still sends no
  credential and no provider headers.
