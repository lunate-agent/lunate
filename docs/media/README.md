# Media set

What `README.md` and the docs embed, where the frames come from, and which build produced
them. The GIFs are manual artifacts in the repository — re-render them through the capture
harness when the UI changes, never leave an asset showing a UI that no longer exists.

## Capture builds

The feature and demo set was captured from a Release build of the repository at **`abb76346`**
(the release notes themselves show `0.1.0+abb7634673df85d08bda7dc83837cbb3f130fc32`, so the
welcome header in those GIFs names this build). The scripted reasoning tape and the demo tape
replay committed fixtures through `LUNATE_REPLAY` with `LUNATE_DATE=2026-10-18` pinned — no
provider is called during those renders.

`setup-wizard.gif` was captured from the `v0.3.0-rc.2` release build
(`0.3.0-rc.2+a8af348`) through the keyless local stub. The stub serves a recorded
OpenCode Go discovery response (`fixtures/setup-wizard-models.json`), so the picker and
model check show real provider and model ids without a network call. It is the only asset
captured from an rc build rather than the older feature-set build.

## Assets

| Path | Shown by | Tape |
| --- | --- | --- |
| `demo.gif` | the README hero | [`tapes/demo.tape`](tapes/demo.tape) |
| `features/input.gif` | [`usage.md`](../usage.md) — input box and autocomplete | [`tapes/features-input.tape`](tapes/features-input.tape) |
| `features/turns.gif` | [`usage.md`](../usage.md) — two turns, user echo, tool block | [`tapes/features-turns.tape`](tapes/features-turns.tape) |
| `features/narrow.gif` | [`usage.md`](../usage.md) — 40-column frame | [`tapes/features-narrow.tape`](tapes/features-narrow.tape) |
| `features/approval.gif` | [`usage.md`](../usage.md) — the approval panel | [`tapes/features-approval.tape`](tapes/features-approval.tape) |
| `features/tools.gif` | [`usage.md`](../usage.md) — tool blocks and `Ctrl+O` | [`tapes/features-tools.tape`](tapes/features-tools.tape) |
| `features/diagnostics.gif` | [`usage.md`](../usage.md) — `cs_diagnostics` after an edit | [`tapes/features-diagnostics.tape`](tapes/features-diagnostics.tape) |
| `features/trust.gif` | [`usage.md`](../usage.md) — the extension trust prompt | [`tapes/features-trust.tape`](tapes/features-trust.tape) |
| `features/extensions.gif` | [`usage.md`](../usage.md) — `/extensions` in a session | [`tapes/features-extensions.tape`](tapes/features-extensions.tape) |
| `features/thinking.gif` | [`usage.md`](../usage.md) — reasoning streaming | [`tapes/features-thinking.tape`](tapes/features-thinking.tape) |
| `features/bash.gif` | kept in the set, not embedded | [`tapes/features-bash.tape`](tapes/features-bash.tape) |
| `features/follow-up.gif` | kept in the set, not embedded | [`tapes/features-follow-up.tape`](tapes/features-follow-up.tape) |
| `setup-wizard.gif` | `docs/first-run.md` — the keyless first-run walk | [`tapes/setup-wizard.tape`](tapes/setup-wizard.tape) |
| `first-run.gif` | not on disk yet — see [`first-run.md`](first-run.md) | recorded by hand with a real key |

A note on the logo and social preview: the brand files do not live in this media set — the
build packs them as package icons out of [`assets/brand/`](../../assets/brand/), a build-owned
directory, so `docs/` stays prose that no build reads (#430).

## Re-rendering

`scripts/render-media.sh` is the only way to produce the GIFs above: it regenerates every
committed tape against a Release build, keyless and deterministic. Requirements and the
re-record procedure (provider key) are documented in the script's header. The frame is always
1400×700, font size 16, padding 12, Dracula (`features-narrow` at 450×620); change it nowhere
else. Every GIF must stay under 5 MB — the README media guard rejects bigger files.

Two tapes drive their moment deliberately: `features-input` filters by text (`/cop` then
`Tab`) instead of counting rows, so it stays correct when the command table changes;
`features-narrow` waits 40 s after the approval so the turn finishes before its `/` list opens.
`features-bash` and `features-follow-up` replay an exchange whose fixture digest no longer
matches, so both show the app's own `run failed: fixture mismatch` notice — the same broken
choreography the previous capture had, not a new defect.

The setup wizard is keyless and isolated, so it has its own harness instead of a replay
fixture: `scripts/render-setup-wizard.sh` starts a local OpenAI-compatible stub and gives
`LUNATE_HOME` a throw-away directory. Render it from this directory with
`LUNATE_BIN=/path/to/lunate vhs tapes/setup-wizard.tape`; the tape writes `setup-wizard.gif`
here. Nothing in the recording reaches a real provider or reads a real key.

## Narrow-width captures

A narrow capture takes its width from the geometry the renderer and the terminal share: the
tape's `Width`, or a real tmux pane/window resize. Setting `stty cols N` inside a pane changes
only the pty's reported width, so the frame and the terminal can disagree about where a line
wraps. When the renderer believes it is wider than the terminal, a line occupies more physical
rows than the writer counted and each repaint leaves the previous copy in the scrollback — a
known way to fake repeated live-area lines. At a width both sides agree on the frame is
idempotent: repainting unchanged state leaves one copy. Reproduce a narrow-width report at that
geometry before treating it as a product defect; `WrappedLiveAreaTests` pins the 40- and
45-column walk.
