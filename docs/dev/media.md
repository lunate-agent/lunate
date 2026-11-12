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

`setup-wizard.gif` was captured from the **published v0.3.0 release binary** —
`lunate-osx-arm64.tar.gz` from the tag's assets, `0.3.0+9bb5edcb650f91c72bbc38e374925e34456e3c31`
— so the welcome header in the asset names the build a user who installs today gets. It
runs through the keyless local stub. The stub serves a recorded OpenCode Go discovery
response (`fixtures/setup-wizard-models.json`) and repoints each model's declared API host
at itself, so the custom-endpoint walk, the connection test and the model check show real
provider and model ids without a network call.

## Assets

| Path | Shown by | Tape |
| --- | --- | --- |
| `demo.gif` | the README hero | [`tapes/demo.tape`](../media/tapes/demo.tape) |
| `features/input.gif` | [`usage.md`](../usage.md) — input box and autocomplete | [`tapes/features-input.tape`](../media/tapes/features-input.tape) |
| `features/turns.gif` | [`usage.md`](../usage.md) — two turns, user echo, tool block | [`tapes/features-turns.tape`](../media/tapes/features-turns.tape) |
| `features/narrow.gif` | [`terminal-setup.md`](../terminal-setup.md) — 40-column frame | [`tapes/features-narrow.tape`](../media/tapes/features-narrow.tape) |
| `features/approval.gif` | [`usage.md`](../usage.md) — the approval panel | [`tapes/features-approval.tape`](../media/tapes/features-approval.tape) |
| `features/tools.gif` | [`usage.md`](../usage.md) — tool blocks and `Ctrl+O` | [`tapes/features-tools.tape`](../media/tapes/features-tools.tape) |
| `features/diagnostics.gif` | [`usage.md`](../usage.md) — `cs_diagnostics` after an edit | [`tapes/features-diagnostics.tape`](../media/tapes/features-diagnostics.tape) |
| `features/trust.gif` | [`usage.md`](../usage.md) — the extension trust prompt | [`tapes/features-trust.tape`](../media/tapes/features-trust.tape) |
| `features/extensions.gif` | [`usage.md`](../usage.md) — `/extensions` in a session | [`tapes/features-extensions.tape`](../media/tapes/features-extensions.tape) |
| `features/thinking.gif` | [`usage.md`](../usage.md) — reasoning streaming | [`tapes/features-thinking.tape`](../media/tapes/features-thinking.tape) |
| `features/bash.gif` | kept in the set, not embedded | [`tapes/features-bash.tape`](../media/tapes/features-bash.tape) |
| `features/follow-up.gif` | kept in the set, not embedded | [`tapes/features-follow-up.tape`](../media/tapes/features-follow-up.tape) |
| `setup-wizard.gif` | [`quickstart.md`](../quickstart.md) — the keyless first-run walk | [`tapes/setup-wizard.tape`](../media/tapes/setup-wizard.tape) |
| `first-run.gif` | not on disk yet — see [`quickstart.md`](../quickstart.md) | recorded by hand with a real key |

A note on the logo and social preview: the brand files do not live in this media set — the
build packs them as package icons out of [`assets/brand/`](../../assets/brand/), a build-owned
directory, so `docs/` stays prose that no build reads (#430).

## Re-rendering

`scripts/render-media.sh` is the only way to produce the demo and feature GIFs above: it
regenerates every tape under `tapes/` against a Release build, keyless and deterministic.
Requirements and the re-record procedure (provider key) are documented in the script's
header. The frame is always 1400×700, font size 16, padding 12, Dracula
(`features-narrow` at 450×620); change it nowhere else. Every GIF must stay under 5 MB —
the README media guard rejects bigger files.

Two tapes drive their moment deliberately: `features-input` filters by text (`/cop` then
`Tab`) instead of counting rows, so it stays correct when the command table changes;
`features-narrow` waits 40 s after the approval so the turn finishes before its `/` list opens.
`features-bash` and `features-follow-up` replay an exchange whose fixture digest no longer
matches, so both show the app's own `run failed: fixture mismatch` notice — the same broken
choreography the previous capture had, not a new defect.

The setup wizard is keyless and isolated, so its tape starts a helper instead of a replay
fixture: [`tapes/setup-wizard.tape`](../media/tapes/setup-wizard.tape) types
`scripts/render-setup-wizard.sh`, which starts the local OpenAI-compatible stub
(`stubs/setup-wizard-provider.py`) and runs `lunate setup` with `LUNATE_HOME` in a
throw-away directory outside the workspace. The tape takes the custom-endpoint route, types
the stub URL and the dummy key `sk-demo`, reaches the summary and confirms the write. The
stub logs every request to `$TMPDIR/lunate-setup-wizard-demo/provider.log`, which is the
proof that no real provider was called. Override the binary with
`LUNATE_BIN=/path/to/lunate`; v0.3.0's asset was recorded from the published release
binary. Nothing in the recording reaches a real provider or reads a real key.

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
