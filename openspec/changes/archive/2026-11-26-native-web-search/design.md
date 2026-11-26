## Context

ADR-0046 records the two-layer web-search decision: the local `web_search` tool with its backend
seam is layer one, and provider-native search is layer two. This change implements layer two behind
an explicit opt-in. The local tool and its `tools.webSearch` settings are not changed; the new layer
changes only what the chat request declares.

The relevant current state:

- `Lunate.Agent` builds every chat request in `AgentHarness.BuildOptions`, from
  `ToolRegistry.Declarations` or the run's active-tool selection.
- `Lunate.Coding` registers both web tools in `HarnessFactory.CreateTools`; the `agent-tools` spec
  requires every harness built by the factory to keep them registered.
- A model's capability facts are already an open feature set on `ModelInfo.Capabilities.Features`,
  layered catalog → discovery → user profile override.
- `Microsoft.Extensions.AI.Abstractions` 10.10.1 already carries `HostedWebSearchTool`, and the
  pinned OpenAI Chat adapter maps it to the provider's own `web_search_options` request field. No
  provider code is needed for this card.
- Approval cannot cover a native search: it runs inside the chat call, so the user must opt in
  once, from a user-scoped setting.

```text
+---------------------------- Lunate.Coding -----------------------------+
| settings.json: tools.nativeSearch (user scope, default false)          |
| HarnessFactory: local web_search + web_fetch stay registered           |
| PrintMode / InteractiveSession / AcpMode -> AgentHarnessOptions        |
+------------------------------------+-----------------------------------+
                                     |
                                     v
+---------------------------- Lunate.Agent -------------------------------+
| AgentHarness.BuildOptions(activeTools, request model)                  |
|   opt-in on?  AND  model features contains "web_search"?               |
|   AND  tool selection offers web_search?                               |
|       yes -> remove local web_search declaration, add HostedWebSearchTool |
|       no  -> unchanged: local web_search declaration                   |
+------------------------------------+-----------------------------------+
                                     |
                                     v
+------------------------- provider chat request -------------------------+
| local function tools + provider built-in search, one chat usage call   |
+-------------------------------------------------------------------------+
```

Assumptions: the request's model is the one the harness resolved for that exchange (the configured
chat usage, or the routed selection); the tool selection is `--tools`/`--no-tools` (or null).

## Goals / Non-Goals

**Goals:**

- One explicit opt-in (`tools.nativeSearch`) turns provider-native search on for a model that
  declares the `web_search` feature.
- The request carries the provider's built-in search and does not declare the local `web_search`
  function; without opt-in or feature the request is byte-for-byte the existing declaration set.
- The decision is made per request from that request's model, so `/model` and routing keep working.
- Billing and attribution stay on the chat call; no usage, cost lane or session entry is added.
- The whole behaviour is provable offline with a stub chat client.

**Non-Goals:**

- No new provider adapter, no provider-specific search parameters, no hosted-search configuration
  beyond on/off.
- No change to the local tool, its backend seam, its approval tier or its result bytes.
- No new usage/attribution value, no `/cost` lane, no session-format change.
- No project-scoped opt-in: a repository must not enable an unapprovable search path.
- No automatic fallback from native search to the local tool (or back) beyond the per-request
  declaration decision.

## Decisions

### 1. A boolean user-scoped setting, separate from the local backend seam

`tools.nativeSearch` is a new boolean beside `tools.extra`; `tools.webSearch` keeps its meaning and
is not extended. The layers stay separate, as ADR-0046 requires: `tools.webSearch` selects the
backend of the local tool, and `tools.nativeSearch` opts into the provider-side layer. The key is
user scope only because a native search cannot be approved per call: a project file may not switch
it on. It has no environment-variable override, matching `tools.webSearch`.

Alternatives rejected: `tools.webSearch.native` would mix the local backend's section with a
different layer; a backend literal such as `backend: "native"` would present the native layer as a
local backend and hide that no local request happens; a top-level `webSearch` section was already
rejected in ADR-0046.

### 2. The model feature is the other half of the condition

The feature name is `web_search` in `ModelInfo.Capabilities.Features`. A model that does not carry
it is not assumed to support native search (unknown is not a yes), so the local tool stays. This
lets the user declare support through a profile's `capabilities.features` or a catalog entry
without a code change, exactly like every other capability fact.

### 3. The request is composed in `BuildOptions`, per exchange

`AgentHarness.BuildOptions` already receives the active tool selection and the request's model
(including a routed model). It is the one place where both facts meet. When the opt-in is on, the
model declares `web_search`, and the selection would offer `web_search`, the method removes the
local declaration named `web_search` from the request's tool list and appends a
`HostedWebSearchTool`. The registry itself is not mutated: the core tool set stays registered, and
a child run or a nested call cannot lose a tool because of a per-request declaration decision.

The tool selection still governs, so `--tools read` declares no search of either kind and
`--tools web_search` declares the native one. This keeps one existing control surface instead of
inventing a second.

Alternatives rejected: removing the tool from the registry at harness build (it breaks `/model`
rebuilds and the core-registration spec, and routing can change the model per exchange); a new
hosted-tool option on the registry (the registry is the local tool set, not the provider request);
filtering the system prompt's tool list (the hosted tool is also named `web_search`, so the prompt's
name remains true).

### 4. Attribution is not touched

The native tool is an `AITool` marker in `ChatOptions`, not a second model call. The existing
`UsageUpdated`, cost and session paths handle one chat call and are unchanged. No usage key is
added to the settings schema and no `changes` entry is needed for attribution.

## Risks / Trade-offs

- **A provider adapter that does not map `HostedWebSearchTool`** -> the model then receives no search
  ability relative to the local tool. The opt-in is per user and the model must declare the feature,
  so the user asked for provider-side search; the adapter's mapping is the provider layer's
  contract. Tests pin Lunate's declaration, not each provider's wire.
- **A profile that overrides `features` with only `web_search`** -> the chat usage's `tools` feature
  requirement fails before the request, because capability categories replace wholesale. This is
  existing capability semantics; the documentation names both the feature and that the `tools`
  feature must remain declared.
- **Routing selects a model without the feature** -> that request falls back to the local tool,
  because the condition is evaluated per request. This is the intended conservative behaviour.
- **A native search is not visible in the approval panel** -> that is the reason for the explicit
  user-scoped opt-in and is recorded in ADR-0046; the local tool remains the visible alternative.
- **The local backend setting can be configured while native search is on** -> it is simply unused
  until the native layer is off or the model no longer declares the feature. It is not an error,
  because the opt-in can be temporary.

## Migration Plan

None: the key defaults to false and no existing setting, session file or tool definition changes.
A user enables it in `~/.lunate/settings.json`; turning it off restores the local tool on the next
request.

## Open Questions

None. ADR-0046 already records the architecture decision; this change implements it and the ADR
manifest records that no new durable ADR is needed.
