## 1. The pure decision (`scripts/ci-issue-triage`)

- [x] 1.1 Add `scripts/ci-issue-triage`: `--jobs-json` prints `id<TAB>name` for every red job in any of
  the three jobs-endpoint shapes; `--milestones-json` prints the lowest open `MAJOR.MINOR.PATCH` title
  (never `backlog`, never a non-version title); `--log-file --source-root` prints one area label or
  nothing.
- [x] 1.2 Implement the area rule: `Lunate.Tui.Tests` → `area/tui`; `Lunate.Coding.Tests` → `area/tui`
  when the test class's source names the `Lunate.Tui` namespace, else `area/core`; anything unclear →
  no label.

## 2. The filing script (`scripts/nightly-issue.sh`)

- [x] 2.1 Take `--event` and word the card body with the event (`push to main`, `nightly`, or the raw
  event name) while the title stays `CI failed: <job>`.
- [x] 2.2 Read the red jobs first and return early on a green run; read the open milestones once and use
  the lowest open version milestone, filing without one (and saying so) when none is open.
- [x] 2.3 Fetch each red job's log, derive the area label from the pure decision, and pass `kind/bug`
  plus the area label to `gh issue create`; a missing log is not a failure.
- [x] 2.4 Keep the duplicate check on the title and the comment path for an open card.

## 3. The workflow

- [x] 3.1 Rename the `nightly-issue` job to `red-main-issue` and run it on `schedule` and `push` with
  `always()`; keep the `actions: read` and `issues: write` permissions and pass the event name.
- [x] 3.2 Update the comments that describe the reporter and the `ci-ok` job set.
- [x] 3.3 Update `scripts/ci-ok.sh --self-test`: the reporter's trigger is the nightly and a push to
  main, and the job name is `red-main-issue`.

## 4. Tests

- [x] 4.1 Extend `scripts/nightly-issue.sh --self-test` with a stubbed `gh` that serves milestones and
  per-job logs: the existing nightly cases keep their meaning, a push red job files a card naming the
  leg and the run, the lowest open version milestone wins, a card is filed without a milestone when none
  is open, the theme-file failure is `area/tui`, another coding test is `area/core`, and an undecidable
  job carries no area label.
- [x] 4.2 Run the failure mode first (the new cases fail before the change) and then the pass, and record
  the counts.

## 5. Docs and record

- [x] 5.1 Update `CONTRIBUTING.md` and the CI description to the push-and-nightly reporter, the
  milestone rule and the area label rule.
- [x] 5.2 Write the changelog fragment `changes/<pull-request-number>.md` under the release's headings.
- [x] 5.3 Run `bash scripts/ci-ok.sh --self-test`, `bash scripts/nightly-issue.sh --self-test`,
  `dotnet csharpier check .` and `openspec validate red-main-push-issue --strict`.
- [ ] 5.4 Run `openspec validate --all --strict`, sync the delta and `openspec archive red-main-push-issue
  --yes` as the final commit.
