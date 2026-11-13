# Setup wizard navigation: back/forward, returning menus, multi-select

## Why

The setup wizard walks ten steps in one direction. A user who reaches the summary and sees a
wrong answer has no way back into the walk except aborting and starting again, a menu that changes
a setting closes after the change so the next switch needs another trip through the step, and the
plural answer (which routing profiles participate) is collected as one implicit "all of them".
S-8 (#680, `docs/spikes/S-8/report.md`) adopted one interaction model for setup; this change
implements its navigation and selection half.

## What Changes

- **A visible path.** Every wizard header states the four-stage path
  (`Provider › Model › Profile › Review`) and the position (`2/4`). The ten steps do not move;
  the stages group them.
- **Back and forward.** `b`/Left returns to the previous step with every answer kept; `f`/Right
  replays only steps already answered and stops at the first one that needs an answer. A walk can
  go back from the summary, change answers and come forward without re-answering the rest.
- **Menus return to themselves.** A menu action returns to the same menu with the changed value
  visible; only **Done** leaves. This generalises the decisions-settings list (the shape #687
  applied there) instead of adding a second menu pattern.
- **Multi-select for plural answers.** Space toggles `[x]`, Enter confirms, and a count line is
  visible while toggling. Used for the routing profiles the walk binds; single-select stays the
  default everywhere else.
- **Esc has one meaning per surface.** Esc at a menu returns to the step that opened it; Esc at a
  question leaves the run with nothing written.
- **Filtering over 15 rows.** Lists longer than 15 rows filter as text is typed; shorter lists do
  not, so `b` and `f` are the verbs there.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the walk gains the path header, back/forward, the returning menu with a **Done**
  entry, and the multi-select grammar.

## Impact

- `src/Lunate.Coding/SetupWizardModel.cs` — stage mapping, answer tracking, walked-range navigation.
- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs` — the walk loop, the decisions menu, the
  routing multi-select.
- `src/Lunate.Coding/InteractiveSession.Commands.cs` — wizard picker verbs (`b`/Left, `f`/Right,
  menu-local Esc) and the >15-row filter rule.
- `src/Lunate.Coding/InteractiveSession.SetupWizardSummary.cs` — the multi-select and menu helpers.
- `src/Lunate.Tui/Interaction/SelectList.cs` — the multi-select count line.
- `openspec/specs/setup-wizard/spec.md` — the new requirement and its scenarios.
- No public API, package, protocol or session-format change.
