## 1. Spec text

- [ ] 1.1 Confirm the delta applies cleanly: `openspec validate question-suggestions-non-interactive --strict`.
- [ ] 1.2 Apply the ADDED requirement "Question suggestions attach only where a person answers" to
      `openspec/specs/decision-layer/spec.md` via this change's archive step (it must not weaken any
      existing SHALL, including the approval seam's rules).

## 2. Pinning tests (behaviour already correct — guard the deliberate scope)

- [ ] 2.1 Extend `tests/Lunate.Coding.Tests/PrintQuestionFallbackTests.cs` with a run configured
      with `decisions.questions: suggest` and an available decision profile: assert the `ask_user`
      degrade text is unchanged and no question decision exchange happens (no suggestion-bearing
      questions, no decision usage call).
- [ ] 2.2 Extend `tests/Lunate.Protocols.Tests/AcpQuestionTests.cs` the same way: suggestions
      enabled plus a decision profile, assert the editor is shown no question and no suggestion, the
      no-user result is returned, and no question decision event appears in the event stream.
- [ ] 2.3 Run the narrow gate — build, the two touched test projects, `dotnet csharpier check .`,
      `openspec validate question-suggestions-non-interactive --strict`.
- [ ] 2.4 Comment the PR with the test results; if the maintainer's sign-off answers accepted either
      queue question, stop and file the follow-up change instead of implementing an invention.
