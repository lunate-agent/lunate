# Full screen

This page answers how to run the interactive session on a screen of its own, and the short answer is
`--fullscreen` for one run or `tui.fullscreen` in the settings file to keep it. The session then
paints on the terminal's alternate buffer and hands back the scrollback the terminal had before it
started. Inline rendering stays the default, because the scrollback is how a finished session is
read after the fact.

## Turn it on

<!-- run: no starts an interactive session on a terminal -->
```bash
lunate --fullscreen
```

The flag turns the mode on for that run. To keep it, set `tui.fullscreen` in
`~/.lunate/settings.json`:

<!-- config: settings -->
```json settings
{
  "schemaVersion": 2,
  "tui": { "fullscreen": true }
}
```

Either source turns the mode on and there is no flag that turns a saved setting off, so remove the
key (or set it to `false`) to go back to inline. The [settings reference](settings.md) owns the
`tui.fullscreen` row and the [command reference](cli.md) the flag.

## What the alternate screen changes

The mode enters the alternate screen after the first frame is painted, so a terminal that cannot
switch buffers still shows a usable session: the enter fails soft, the session stays inline, and one
line says `full screen unavailable: <reason>`.

While the mode is on, the transcript and the input box paint on the alternate screen. The transcript
keeps its scrollback there, inside the session; when the session ends the terminal's own scrollback is
what it was before, and the session does not copy its transcript back into it.

`Ctrl+O` still opens the transcript viewer, and so does the `/fullscreen` command that duplicates
it: the session leaves its screen first, the viewer enters its own and returns it on close, and the
session enters again — one enter and one leave per buffer, so the terminal is never left in the
alternate buffer. The command and this page's mode are two different things: the command opens a
view, `--fullscreen` moves the session itself.

## Leaving

Every exit path leaves the alternate screen, so it is never left behind:

- a normal quit — `/quit`, or `Ctrl+D` twice on the empty prompt
- `Ctrl+C` twice on the empty prompt
- an unhandled exception: the leave is written before the exception escapes the session

No new key is added: the quit window that ends the inline session ends the full-screen session too,
and the leave is the last thing the session writes.

## Related

- [Usage](usage.md) — the interactive session, its turn output and its quit keys
- [Terminal setup](terminal-setup.md) — the terminals that support the alternate screen
- [Settings reference](settings.md) — the `tui.fullscreen` row
- [Command and option reference](cli.md) — `--fullscreen` beside the other launch flags
- [Themes](themes.md) — the colours the full-screen session paints with
