# The setup wizard, recorded for docs/media/setup-wizard.gif.
#
# Re-record it with `scripts/render-media.sh setup-wizard` (or without an argument for the whole
# media set): that script is the only renderer, and this tape is the wizard's only home. The tape
# starts scripts/render-setup-wizard.sh in the terminal, which serves the recorded OpenCode Go
# discovery response from the keyless local stub, points LUNATE_HOME at a throw-away directory
# outside the workspace, and runs `lunate setup` in the scratch project. Nothing reaches a real
# provider and no real credential is read; the only key typed is the obvious dummy "sk-demo".
#
# Timing, not `Wait`: the wizard draws a full screen, so VHS's Wait matches escape sequences rather
# than the words on screen (it reported only the last line). The margins are generous because the
# recorder pays for Chrome plus a cold .NET start before the first prompt appears.
#
# Pickers filter as text is typed, so the tape names the row it wants instead of counting Down
# presses: the preset list, the discovered model list and the credential menu all stay reachable
# when their contents change.
Output setup-wizard.gif

Set FontSize 16
Set Width 1400
Set Height 700
Set Padding 12
Set Theme "Dracula"

Hide
Type "bash ../../scripts/render-setup-wizard.sh"
Enter
Sleep 8s
Show

# Provider: filter the preset list to the custom-endpoint row.
Type "Custom"
Sleep 1s
Enter
Sleep 2s

# The custom path: the id, then the stub's URL so discovery and the checks are local.
Type "opencode-go-local"
Enter
Sleep 1s
Type "http://127.0.0.1:18081/v1"
Enter
Sleep 2s

# Family (openai-chat) and credential placement (bearer) are the picker defaults.
Enter
Sleep 1s
Enter
Sleep 1s

# Region is free-form; the optional models URL is left empty and discovery uses <endpoint>/models.
Type "local"
Enter
Sleep 1s
Enter
Sleep 2s

# Credential: the first row is "enter a key"; the typed key is an obvious dummy and is never checked.
Enter
Sleep 2s
Type "sk-demo"
Sleep 1s
Enter
Sleep 2s

# Connection test: run it (the default row) against the stub.
Enter
Sleep 4s

# Model: filter the discovered listing to a chat-completions model whose id is not shared by
# other providers in the built-in catalog (a bare id that matches several wire ids is ambiguous).
Type "mimo-v2.6-flash"
Sleep 1s
Enter
Sleep 2s

# Model check: the default row is skip, so move up to send it.
Up
Sleep 500ms
Enter
Sleep 3s

# Profile name; this model declares no effort levels, so no thinking picker follows.
Type "demo"
Enter
Sleep 2s

# Bind the chat profile as the fast usage (the default row is skip).
Down
Sleep 500ms
Enter
Sleep 2s

# Decision profile: skip; decisions settings: keep the defaults.
Enter
Sleep 1s
Enter
Sleep 2s

# The summary is the screen this asset exists to show: every collected value, the passing connection
# test and model check, and the write it is about to perform.
Sleep 2s

# Confirm the write, so the recording ends on a configured session rather than a summary that was
# never accepted. The notice and the status line below the summary name the resolved profile.
Enter
Sleep 5s

# Leave the session so the stub shuts down; the frame above stays the last visible one.
Hide
Type "/quit"
Enter
Sleep 2s
