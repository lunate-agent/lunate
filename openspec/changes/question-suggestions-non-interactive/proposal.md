## Why

Review of the decision layer (#439) found that the question-suggestions consumer has no
non-interactive surface: `QuestionSuggestionsEnabled` and `QuestionDecisionAsync` are wired only by
the interactive session (`src/Lunate.Coding/InteractiveSession.cs`), while print mode and ACP build
their harness options without them (`PrintMode.cs`, `AcpMode.cs`). That is deliberate and nothing
misbehaves — in print mode and ACP, `ask_user` degrades to its no-user result before any question
could be displayed, so a suggestion has nothing to attach to — but the decision-layer spec never
says so. A reader wiring print mode or judging the ACP gap cannot tell deliberate scope from an
unfinished consumer (#446). The spec should name the scope; the open ACP protocol question belongs
to a future design, not to silent omission.

## What Changes

- **Codify the deliberate scope in the decision-layer spec**: question suggestions attach only where
  a question is displayed to and answered by a person — the interactive session. Non-interactive
  surfaces (print mode, unattended queue runs, ACP) propose nothing attached to `ask_user`; the tool
  keeps degrading to its no-user result, and enabling `decisions.questions: suggest` with no
  interactive session produces no question decision call at all.
- **Name the non-interactive consumer that already exists**: the information-only `decide` tool
  remains the way an unattended run obtains a judgement with its probability and provenance; this
  change does not duplicate it inside `ask_user` output.
- **Keep the fail-closed escalation rule untouched** (`openspec/specs/decision-layer/spec.md`,
  "Approval gate escalates, never blocks"): it names the approval gate; question suggestions stay
  outside that seam, as the spec already requires ("Calls that do not enter the approval seam,
  including `ask_user`, SHALL NOT be evaluated by the approval-gate consumer").
- **No ACP protocol surface.** A suggestion rendered beside a question in ACP needs an `ask_user`
  surface between adapter and editor to exist first — a contract change. It is deferred, and its
  two open questions are raised for sign-off (see design.md), not invented here.
- **Pin the decision with tests**: a print-mode and an ACP run configured with
  `decisions.questions: suggest` and a decision profile assert that no question decision is proposed
  and the no-user degrade is unchanged. The degraded-message tests already exist
  (`PrintQuestionFallbackTests`, `AcpQuestionTests`); these extend them to the suggestions consumer.
- Keep this change design-only: it changes spec text and adds the pinning tests; no consumer wiring
  changes.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `decision-layer`: The Consumers requirement gains the deliberate wiring scope — question
  suggestions attach only where a person answers; unattended runs propose none and receive decision
  information through the `decide` tool.

## Impact

- `openspec/specs/decision-layer/spec.md` — spec text only; no existing SHALL is weakened.
- `tests/Lunate.Coding.Tests/PrintQuestionFallbackTests.cs` and `tests/Lunate.Protocols.Tests` —
  test additions pinning the scope; no product code changes, no new NuGet packages, no public API
  change, and no session-format change. The interactive consumer and its suggestion fallbacks
  (#344's wiring and `QuestionSuggestionFallbackTests`/`QuestionDecisionAvailabilityTests`) are
  unchanged.
