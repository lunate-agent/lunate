# Tasks — setup-wizard-modes

One task group: the modes, the review block and their tests ship together as one mergeable slice.

## 1. Modes on the same walk

- [x] 1.1 `SetupWizardModel`: Quick/Detailed mode defaulting to Quick, a toggle that keeps answers,
      mode-aware next-step selection and the left-at-default settings list.
- [x] 1.2 Provider step: the mode row above the presets (default selection unchanged); Quick's
      custom path asks id and endpoint only, Detailed keeps family/placement/region/models URL.
- [x] 1.3 Chat profile step: Quick does not ask thinking level; Detailed does.
- [x] 1.4 Summary: Quick renders one left-at-default line per hidden setting with its default and
      change-later command; Detailed renders none. Scoped entries stay detailed.
- [x] 1.5 Tests: model (default, toggle keeps answers, next-step skips, left-at-default set, scoped
      entry), summary (every Quick default named), session (Quick reaches a working configuration;
      Detailed reaches thinking/fast/decision/decisions; switching keeps answers).
- [x] 1.6 Update `docs/first-run.md` and validate the change with `openspec validate --strict`.
