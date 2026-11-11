# Tasks — setup-wizard-navigation

Three slices. Each compiles, passes the gate and lands on its own; the change archives when the
last one does.

## 1. Path, back and forward

- [x] 1.1 `SetupWizardModel`: `SetupStage`, `StageOf`, `StagePosition`, `HeaderLine`, `HasAnswer`,
      `NextStep`, `PreviousStep`, `FirstUnansweredAfter`; unit tests for each.
- [x] 1.2 `WizardMove.Forward` in the driver; the step pickers map the back/forward sentinels
      through one helper so `null` still aborts.
- [x] 1.3 The wizard's picker keys: Left/Right as verbs always, `b`/`f` while no filter is typed,
      Esc menu-local (a menu returns to its parent; a question ends the run).
- [x] 1.4 Filtering only above 15 rows, so a short list keeps `b` and `f` free.
- [x] 1.5 Tests: back from the summary, change two answers, Forward returns to the summary without
      re-asking the unchanged steps; Forward stops at an unanswered step; the header names the stage
      and position.

## 2. Menus that return

- [x] 2.1 One `MenuAsync` helper: rows, a **Done** row, the action, then the same menu with the
      changed value visible and the cursor on that row.
- [x] 2.2 The decisions-settings step becomes the menu: approvals, routing, questions, tool, Done;
      all-defaults still records nothing; Esc returns to the previous step.
- [x] 2.3 Tests: an action returns to the menu and the row shows the change; Enter on Done leaves;
      Esc leaves the menu for the walk; the staged answers reach the settings file.

## 3. Multi-select

- [x] 3.1 `SelectListRenderer.QuestionLines`: a `<n> selected` line for a pending multi-select,
      fitting every pinned width; unit test.
- [x] 3.2 `AskMultiSelectAsync` in the session; the routing question asks which candidate profiles
      participate, with Space toggling and Enter confirming.
- [x] 3.3 Tests: three candidates, two toggled with Space, count visible, exactly those two written
      as `decisions.routingProfiles`.

## 4. Spec and docs

- [ ] 4.1 Archive the change so `openspec/specs/setup-wizard/spec.md` carries the new requirement.
- [x] 4.2 No document outside `openspec/` states the wizard's key handling today; nothing else
      changes.
