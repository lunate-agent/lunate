# Key bindings

The prompt's bindings, one meaning each; [the guide](dev/guide.md) describes the components and layout
they drive.

**Key bindings (one meaning each)**

<!-- keys:start -->
| Key | Action |
| --- | --- |
| `Enter` | Send; with the autocomplete list open, accept the selected item first (an exact token sends instead); in bash mode, run the line through the `bash` tool; while a turn runs, queue the line as a follow-up (the footer shows `N queued`). Never steers or interrupts a turn |
| `Alt+Enter` / `Shift+Enter` / `Ctrl+J` / `Ctrl+Enter` | New line in every state (the alternatives are for terminals that report extended keys or send the control byte); the autocomplete triggers re-run afterwards. Never submits or queues |
| `Esc` | Close the autocomplete list first; in bash mode, leave without running and restore the line with its prefix; otherwise interrupt the running turn, whose queued follow-ups then run next; with nothing running, leave the queue alone |
| `Ctrl+C` | Interrupt the running turn (a queued follow-up then runs next); with the prompt idle it clears the input, and on an empty idle prompt a second press within 2 s quits |
| `Ctrl+D` | Quit on a second press within 800 ms, only from an empty idle prompt — never mid-turn |
| `Ctrl+L` | Force a full redraw of the prompt area, keeping the input and the conversation |
| `Ctrl+P` | Unbound: nothing happens. It is deliberately left free (the model picker is `Alt+P`), and the keybindings reference lists it so a reader does not have to guess |
| `Alt+P` | Model picker and search; typing filters the catalog, Backspace edits the filter, Esc clears it before closing |
| `Ctrl+R` | History search: typing narrows, `Ctrl+R`/`Down` moves to the next match, `Enter` places it in the input, `Esc` cancels |
| `Ctrl+S` | Stash the draft, or restore it when the prompt is empty |
| `Ctrl+G` | Open the draft in your editor: `$VISUAL`, else `$EDITOR`, else `vi` on POSIX and `notepad` on Windows — arguments are split on whitespace, with no shell and no quoting. A zero exit replaces the draft with the file's text and puts the cursor at its end; the program must wait for the file (`code --wait`), or the draft is read before the edit lands. Some terminals and tmux configurations swallow `Ctrl+G` (it is `BEL`, `0x07`); there `/editor` runs the same round-trip, on the draft the command leaves — an empty one |
| `Ctrl+_` | Undo the last input edit |
| `?` | On an empty idle prompt, open the key-help overlay: the bindings that apply here, grouped by state; `j`/`k` move, `/` filters, `Tab` shows every binding, `Esc` or `?` closes without touching the draft. In an approval prompt it shows that panel's key legend — `y` approves, `n` denies, `a` approves for the session, `Space` activates the selection, Enter and Esc deny — and decides nothing. With text in the prompt it is a literal character |
| `y` / `n` / `a` | Answer a pending approval: approve, deny, or approve for the whole session. The panel owns these letters, so nothing else sees them while a question is open |
| `Ctrl+O` | Transcript viewer: the session transcript with full tool output and diffs; `q`, `Esc` or `Ctrl+O` closes. Refused while an approval or picker is open |
| `Ctrl+X` | Copy the last answer to the clipboard; the notice says whether a platform tool confirmed the write or the OSC 52 sequence was sent, and names tmux's `set-clipboard on` when it applies |
| `Tab` | Accept the selected autocomplete item; otherwise nothing |
| `Up` / `Down` | Move the autocomplete selection; with the list closed, history (bash mode recalls its own command stream). With queued follow-ups, `Up` takes the whole queue back into the editor instead |
| `Ctrl+A` / `Ctrl+E` | Move to the start / end of the line |
| `Ctrl+K` / `Ctrl+U` | Kill from the cursor to the end / to the start of the line |
| `Ctrl+W` / `Alt+D` / `Ctrl+Delete` | Kill the word before / after the cursor (`Ctrl+Delete` is the Alt-free form of `Alt+D`) |
| `Ctrl+Y` | Yank the most recent kill |
| `Alt+B` / `Alt+F` / `Ctrl+Left` / `Ctrl+Right` | Move back / forward by word (`Ctrl+Left`/`Ctrl+Right` are the Alt-free forms) |
<!-- keys:end -->
