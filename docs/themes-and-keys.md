# Change the theme and remap a key

This page answers how to change the terminal's colours and rebind an input key, and the short answer
is `/settings`: `tui.theme` chooses the colour-depth policy and `tui.keys <binding id> <key>` rebinds
one key, both persisted so they survive a restart.

## Choose a theme

`/settings tui.theme` chooses the built-in theme for the session: `default` and `dark` paint the
shipped palette and keep the terminal's detection, `light` paints dark inks for a light-background
terminal, `ansi16` pins the high-contrast sixteen-colour system, and `mono` drops every attribute.
The choice persists in the settings file's `tui` section, and a [theme file](themes.md) can recolor
the tokens of whichever built-in is selected. A theme can only lower the depth, so `NO_COLOR` still
wins.

## Remap a key

`/settings tui.keys <binding id> <key>` remaps a binding: the catalogue row keeps its id, intent,
context and description and gains the one key — `keys.json` stays the only place a key is declared.
An unknown id is refused with the closest ids named, a key an earlier row already answers is refused
with that row named, and any remap persists so it survives a restart.

The binding ids come from the [key binding table](keybindings.md).

## A keymap file

`keymap.json` in the Lunate home (`~/.lunate/keymap.json`; `$LUNATE_HOME` moves the home wholesale)
overlays the shipped catalogue by binding id. `$LUNATE_KEYMAP` names another file for every run, and
`--keymap <path>` names one for a single run; the flag wins over the variable, and the variable over
the home file.

The file keeps `keys.json`'s top-level shape: `version` and a `bindings` array. Each entry names the
catalogue `id` it changes and the `keys` that binding should answer with after the load — nothing
else. The intent, context and description stay the catalogue's, so a file cannot invent an action or
a place for it to apply.

```json
{
  "version": 1,
  "bindings": [
    { "id": "ctrl-l.redraw", "keys": [{ "kind": "F5" }] },
    { "id": "ctrl-d.quit", "keys": [] }
  ]
}
```

A key is an object with a `kind` — `Character`, `Space`, `Enter`, `Tab`, `Backspace`, `Delete`,
`Escape`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, `Up`, `Down`, `Left`, `Right`, `F1` to
`F12`, or `Paste` — plus optional `ctrl`, `alt` and `shift` booleans and the `text` a `Character`
carries. An empty `keys` array disables that binding, which is how a default is turned off without
inventing a new intent.

An id the catalogue does not have fails the load and names the closest catalogue ids; so does a
binding id given twice, or two of the file's own bindings that answer the same key in the same
context. When the file takes a key from a catalogue binding, the file wins and startup prints one
line naming both ids. A file that fails anywhere is discarded whole, leaving the shipped keys in
place, rather than half-applied.

[The example file](examples/keymap.json) rebinds redraw to F5 and disables the quit binding.

## Related

- [Themes](themes.md) — the built-in themes, the token file and the colour downgrade
- [Settings reference](settings.md) — the `tui.theme` and `tui.keys` rows
- [Usage](usage.md) — the keys the prompt and the transcript use
- [Run tool calls in a container](execution-environments.md) — the same session inside one container
