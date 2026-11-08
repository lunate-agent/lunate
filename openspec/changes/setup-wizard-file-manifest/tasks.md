# Tasks

The change is one review of the existing staged write; it ships as one slice: the renderer, its two
call sites, the 40-column pins and the docs. The commit path is unchanged.

## 1. Manifest and receipt renderer

- [ ] 1.1 Add a structured write list (`file`, `label`, `value`) derived from the staged model, with
      the same provider-target predicates `SetupWizardCommit` uses.
- [ ] 1.2 Render the manifest grouped by `providers.json`, `auth.json`, `settings.json`, one fact
      per line, wrapping values with `CellText.Wrap` to the available width.
- [ ] 1.3 Render the **Left at default / unchanged** block for skipped, carried and defaulted
      settings, each with its change-later command or file.
- [ ] 1.4 Render the short post-write receipt: one line per touched file, the change-later commands
      and the return to the session; a staged no-op says nothing was written.

## 2. Call sites

- [ ] 2.1 The interactive summary renders the manifest at `_console.Size.Columns`; the confirmed
      commit writes the receipt to scrollback.
- [ ] 2.2 `lunate setup` prints the manifest and the receipt, replacing its old `written:` lines.

## 3. Tests

- [ ] 3.1 A rich staged model maps every write to its expected file; no staged value is unlisted.
- [ ] 3.2 A typed custom key is mapped to `auth.json` and its provider section to `settings.json`.
- [ ] 3.3 The changed summary/commit tests pin the manifest and the unchanged block.
- [ ] 3.4 A real terminal-stack walk renders both the manifest and the receipt at 40 columns; no row
      overflows and each file/setting is named.
- [ ] 3.5 A secret never appears in either surface.

## 4. Docs and gate

- [ ] 4.1 `docs/first-run.md` describes the file manifest, the unchanged block and the receipt.
- [ ] 4.2 `openspec validate setup-wizard-file-manifest --type change --strict` passes.
