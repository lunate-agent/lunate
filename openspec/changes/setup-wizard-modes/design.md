# Design — setup wizard: quick and detailed modes

## Shape

Both modes are the same walk, driven by `SetupWizardModel`. The model carries the chosen mode and
answers a single question for the driver: which step comes next. Quick leaves the detailed-only
steps at their defaults; it does not fork the driver, the summary or the commit.

```text
Provider step ── mode row: Quick (default) / Detailed
      │
      ├─ Quick    : credential → connection test → model → model check → chat profile ─┐
      │             custom provider asks id + endpoint only                           │
      └─ Detailed : credential → connection test → model → model check → chat profile ─┤
                    → thinking level (inside the chat profile step)                    │
                    → fast profile → decision profile → decisions settings ───────────┤
                                                                                       ▼
                                                              review (summary) + confirm + one commit
```

## Decisions

- **Mode lives in the model, not the driver.** `SetupWizardMode` is a property of
  `SetupWizardModel`; `RecordMode`/`ToggleMode` changes it without touching any answer. The driver
  asks `Next(step)` for the next walked step, so Quick's skip list is one place and is testable
  without the terminal.
- **Quick leaves no marker in the skipped set.** `IsSkipped` means "the user skipped this step".
  Mode defaults are a different fact, rendered by `LeftAtDefault`; this keeps the existing
  skipped-step summary intact for detailed walks and avoids stale skips when the mode changes.
- **Only the full walk has modes.** Scoped `/setup` entries keep their existing step range
  (`AddProfile` still reaches the decision profile); `IsQuick` is false for every entry except
  `WalkEverything`.
- **The mode row is on the provider step.** It sits above the presets; the picker still opens with
  the first preset (or the carried provider) selected, so the common Enter path is unchanged.
  Selecting the row toggles the mode and re-renders the step; no answer is cleared.
- **Quick's custom provider takes the essentials.** Id and endpoint are required to make any request;
  API family (`openai-chat`), placement (bearer), region and models URL are left at defaults and
  named in the review. Detailed asks them in the existing order.
- **The left-at-default block is model data.** `LeftAtDefault` returns setting, default value and
  the command that changes it later; the summary renders one line per setting. It is empty for
  Detailed and for scoped entries.
- **No new interaction primitives.** The mode row uses the existing provider picker; the review is
  the existing scrollback summary; the commit is unchanged.

## Tests

- Model tests: Quick is the default; toggling keeps answers; `Next` skips the detailed-only steps in
  Quick and walks them in Detailed; `LeftAtDefault` names every hidden setting and is empty in
  Detailed; scoped entries stay detailed.
- Summary test: a Quick walk's review contains one left-at-default line per hidden setting, each
  naming its default and `/setup`, `/settings` or the file path.
- Session tests through the real component stack: Quick reaches a working configuration and writes
  the stores; Detailed asks thinking, fast, decision and decisions after the same first steps;
  switching mode keeps collected answers.

## Risks

- Quick's custom provider defaults to `openai-chat`/bearer, so an Anthropic-compatible endpoint
  typed in Quick would need Detailed for `anthropic-messages`/`x-api-key`. The review names both so
  the trade-off is visible rather than silent.
- Existing walk fixtures that assume the detailed steps now select Detailed explicitly; the new
  Quick default is pinned by its own test rather than inferred from older fixtures.
