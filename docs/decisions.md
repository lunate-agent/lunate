# Let a second model help decide

This page answers how to add the optional decision layer and what it may decide. The short answer:
`decisions` names one profile and enables consumers one at a time, and the layer can only tighten an
approval, never grant one.

## Turn it on

<!-- config: settings -->
```json settings
{
  "schemaVersion": 2,
  "profiles": {
    "chat-main": { "provider": "work-anthropic", "model": "claude-sonnet-5-5" },
    "reviewer": { "provider": "work-anthropic", "model": "claude-opus-5-5" },
    "quick": { "provider": "work-anthropic", "model": "claude-sonnet-5-5" }
  },
  "usages": { "chat": "chat-main" },
  "decisions": {
    "profile": "reviewer",
    "approvals": "enforce",
    "approvalsThreshold": 0,
    "routing": "shadow",
    "routingProfiles": ["chat-main", "quick"],
    "tool": "off",
    "questions": "suggest"
  }
}
```

Decision consumption is configured by one `decisions` block, with the same shape for every consumer.
It is part of `settings.json` (JSON only — nothing reads YAML), shown in the example above. Without
`profile` the layer is unavailable and nothing is offered; enabling a consumer without a profile is
not an error — the layer stays off. The generated [configuration reference](configuration.md)
lists every accepted settings key with its type, default, scope and allowed values.

Every switch is shown with the value it holds now and one explanatory line in the setup wizard; the
list stays open after each change and refreshes every row, and **Done — leave the decisions
settings** leaves it. The same keys can be edited in the file directly.

## What each consumer does

`decisions` in `settings.json` names one profile and opts into any of four consumers: approvals
(`off`, `shadow`, `enforce`, with a threshold for how sure a deny must be), routing (`off`, `shadow`,
`enforce` over two or more profiles), the information-only decide tool, and display-only suggestions
beside a question's top option. A decision can only tighten an approval, never grant one; without a
profile the layer stays off. The same gate runs in the TUI, print mode and ACP, and the
[configuration reference](configuration.md) lists every key.

`routing` records a suggested profile without changing the configured chat model in shadow, or uses
the selected profile in enforce; routing picks the most probable profile and uses no threshold. An
unavailable decision profile or an invalid response leaves the configured chat model in use.
Decision spend is charged to the `decision` usage and `/cost` distinguishes the `native` and
`adapter` methods. The approval gate acts by escalation only: with `enforce`, a risk signal whose
chosen `yes` answer reaches `approvalsThreshold` confidence turns the ask into a user prompt (and
denies it when no user is present); below the threshold the ordinary approval flow runs unchanged.
The key is a confidence, not a probability — `0.5` there means a `0.75` probability of the risk —
and its default `0` is the confidence form of the shipped `0.5` probability gate.

With `questions: suggest`, the decision model adds a probability marker beside its top option. The
user still chooses; the suggestion never selects an option, answers a skipped question, or approves a
tool call. Without a configured decision profile, the ordinary question picker remains available and
the layer reports unavailable without an error state.

Decision use is the user's choice, not a model-intelligence verdict. The consumer is off by default
and must be enabled in user settings. Any configured model may be used; known structured-output
support is checked as a technical requirement, while unknown capability facts are allowed. A
decision response must name a defined option and provide valid probabilities; a malformed response
is retried once and then fails with the model named. Provenance labels `native` or `adapter` and
records the model id. Decisions can only tighten approvals: they never override deny or approve
outside the user's allow envelope. See [`guide.md`](guide.md) for the full model and cost behavior.

## Confidence and approval thresholds

For a binary decision with probability `p`, confidence is `|2p - 1|`. An existing approval
probability threshold `t` of at least `0.5` maps to `2t - 1` in confidence, so the default threshold
`0.5` remains `0`; the approval gate keeps the same behavior while using confidence consistently.

## System One decision profiles

`apiFamily: systemone` selects the native decision transport instead of a chat client. Two built-in
presets declare it:

- `openrouter` — `baseUrls.systemone` is `https://openrouter.ai/api/v1/systemone` and serves
  `typesafe/jev-1.14`, including box questions with an image.
- `opencode` — `baseUrls.systemone` is `https://opencode.ai/inference/systemone/v1/systemone` and
  serves `jev-1.13` only. It is the Console/Zen System One surface; `opencode-go` declares the same
  family on the same URL, and a Go key is accepted there but bills to the pay-as-you-go balance,
  not the Go plan. `jev-1.13` does not support box questions. Image questions need the family
  header `TypeSafe-Danger-Zone-Images: true`, which both presets send.

A decision profile must name an exact version. An alias — an id starting with `~` or ending in
`latest` — is refused with the exact version the catalog records for it, because a provider upgrade
can change probabilities behind the alias. Chat usage with a `systemone` profile is refused by name;
use `decisions.profile` for decisions and an ordinary chat profile for chat.

Decision profiles resolve credentials exactly like chat profiles. For a provider entry whose
`credentialReference` is `auth:<key>`, the provider kind's environment variable — `OPENROUTER_API_KEY`
for an `openrouter` entry, `OPENCODE_API_KEY` (`OPENCODE_GO_API_KEY` works as the alias) for an
`opencode` one, and so on — wins over the key
stored under `<key>`, on the native System One transport and on an adapter alike. An
`env:<VARIABLE>` reference reads only that variable and refuses when it is unset. This precedence is
deliberate: an exported key in a CI environment replaces a stale stored key without editing
`auth.json`; `lunate auth list` shows the source in use.

<!-- config: settings -->
```json settings
{
  "schemaVersion": 2,
  "profiles": {
    "decision": {
      "provider": "openrouter",
      "model": "openrouter/typesafe/jev-1.14",
      "apiFamily": "systemone"
    },
    "chat": { "provider": "openrouter", "model": "openrouter/anthropic/claude-fable-5.1" }
  },
  "usages": { "chat": "chat" },
  "decisions": { "profile": "decision", "tool": "on" }
}
```

Confidence is computed from the answer distribution (`|2p − 1|` for yes/no, the choice/score
normalisation, or the box confidence) and recorded on every decision. Routing changes the chat
profile only when confidence reaches `decisions.routingMinConfidence` (default `0.5`). The approval
gate reads `decisions.approvalsThreshold` on the same confidence scale: a risk signal escalates when
the model chose `yes` and that answer's confidence reaches the threshold, so `0.5` means a `0.75`
probability of the risk. The default `0` is the confidence form of the previously shipped `0.5`
probability gate (`confidence = 2 × probability − 1`), so behaviour is unchanged unless the key is
set. A request above roughly 28k tokens of the 32k per-question budget warns before sending, and
tool results are bounded to head and tail rather than trimmed to a single edge.

Decision usage records the provider's `usage` block, including OpenCode's `usage.opencode` block, and
marks the cost provider-reported. `/cost` shows the decision line with model, method (`native` or
`adapter`) and amount; the catalog estimate is used only when the provider omits `cost`.

## Related

- [Models and providers](models-and-providers.md) — profiles, credentials and the chat model
- [Configuration reference](configuration.md) — the decision keys and their scopes
- [Use the terminal](usage.md) — where approvals and questions appear
