# Design — Setup wizard navigation

Binding implementation detail for the spec delta in `specs/setup-wizard/spec.md`. Where this file
pins a behaviour, apply follows it; a contradiction with the delta is a stop-and-report.

## 1. Stages over the existing ten steps

`SetupWizardModel` keeps one order (`Order`) and the entry's walked range (`FirstWalked`,
`LastWalked`). A stage is a pure function of the step:

| Stage | Steps |
| --- | --- |
| `Provider` | provider, credential, connection test |
| `Model` | model, model check |
| `Profile` | chat, fast, decision, decisions settings |
| `Review` | summary |

`StageOf(step)` maps it; `StagePosition` is the stage's 1-based index. The header line is
`setup — Provider › Model › Profile › Review   <n>/4`, emitted through the existing `Notify` slot at
the top of every step. It is one line, 49 columns, so the 60-column pin still holds.

## 2. Answers and navigation

An answer is a step the walk has already left with a value or an explicit skip:

- provider, credential, chat profile, decision profile, decisions: the staged value or
  `IsSkipped(step)`;
- connection test, model check: a recorded `WizardCheckResult`;
- model: the staged model; fast: `FastProfile` or skipped.

`SetupWizardModel` exposes:

- `NextStep()` — the next walked step, or the summary after `LastWalked`;
- `PreviousStep()` — the previous step, clamped at `FirstWalked` (an entry never jumps before its
  scope);
- `FirstUnansweredAfter(step)` — the first walked step after `step` without an answer, else the
  summary. This is Forward: answered steps are replayed (named in a notice), not re-asked.

`Record*` already clears the answers a change invalidates (a new provider clears the credential,
model and profiles). Forward therefore cannot skip into a cleared step — it is unanswered again.

## 3. The driver

`WizardMove` gains `Forward`. Every step's picker maps the two sentinels the picker can return
(`WizardBackValue`, `WizardForwardValue`) through one helper, so `null` still means abort:

- `Back` → `wizard.Enter(wizard.PreviousStep())`
- `Forward` → notify each kept step, then `wizard.Enter(wizard.FirstUnansweredAfter(wizard.Step))`
- `Advance` → `wizard.Enter(wizard.NextStep())`

The existing go-back after a failed connection test or model check becomes one use of `Back`.

Keys are interpreted in the picker's key handler, and only while a wizard pick is pending:

- Left and Right are always verbs (they mean nothing in a picker today).
- `b` and `f` are verbs in a **menu**, and in a question only while its filter is empty — a typed
  filter owns printable characters.
- Esc clears a non-empty filter first (existing rule); in a menu it returns to the parent step; in a
  question it ends the run.

## 4. Returning menus

One helper, `MenuAsync`, owns the shape: it opens a picker whose last row is **Done**, runs the
chosen action, then re-opens the same picker with the action's row showing the new value and the
cursor back on that row. The decisions-settings step is its first user and the only menu in this
change; the configured-install entry menu keeps its one-shot shape until #683/#686 rewrite it.

`WizardDecisionsChoice` is accumulated across the menu's actions, and a menu left with every value
at its default records `null` (nothing written), as today.

## 5. Multi-select

The TUI already has `SelectListQuestionModel(multiSelect: true)` with Space toggling and `[x]`
rendering. Two additions:

- `SelectListRenderer.QuestionLines` emits `<n> selected` under the description of a pending
  multi-select question, so the count is visible while toggling.
- `AskMultiSelectAsync` in the session wraps that component for the walk, returning the selected
  labels, `null` on Esc.

The routing question uses it when the decisions menu enables routing: the candidates are the chat
profile, the decision profile and the settings file's existing routing profiles; selecting none is
refused with the existing warning (routing needs at least two) and the menu stays open.

## 6. Filtering

`PickerFiltersOn()` returns true only when the unfiltered list has more than 15 rows, for the model,
provider and wizard-choice kinds. Shorter lists do not filter, which is what frees bare `b` and `f`
for the verbs. The model picker keeps its typed search whenever the listing is longer than 15 rows;
the discovery fixtures in the tests use short listings and drive the list with arrow keys.

## 7. Tests

| Behaviour | Test |
| --- | --- |
| stage path and position | model unit test on `StageOf`/`StagePosition`; one driver test reading the header notice |
| back keeps answers; forward replays only answered steps | driver test: walk to summary, Back to chat, rename, Back to decisions, change a switch, Forward → summary; assert staged model + written files |
| forward refuses an unanswered step | driver test: Back past the model, clear it (provider change), Forward stops at the model |
| menu returns with the change; Done leaves; Esc returns to parent | driver tests sending Down/Space/Enter sequences against the decisions menu |
| multi-select count | TUI unit test on `QuestionLines`; driver test toggling three routing profiles with Space and asserting the written routing profiles |
| filtering threshold | unit test on the picker's filter predicate |

Tests drive keys (`KeyEvent` sequences) and assert the staged model, the store files and the
rendered control state (`[x]`, counts), never the prose of a prompt.
