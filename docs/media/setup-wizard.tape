# The setup wizard, recorded for docs/media/setup-wizard.gif.
#
# Run it through scripts/render-setup-wizard.sh and never by hand: the script starts the keyless stub,
# points LUNATE_HOME at a throw-away directory outside the workspace (the binary refuses a home inside
# it) and fixes the stub's port. The walk takes the custom-endpoint route so the connection test and
# model check hit the committed stub, never a real provider. The only secret typed is "sk-demo"; only
# the credential prompt masks it, and the stub does not check it.
#
# Timing, not `Wait`: the wizard draws a full screen, so VHS's Wait matches escape sequences rather
# than the words on screen (it reported only the last line). The margins are generous because the
# recorder pays for Chrome plus a cold .NET start before the first prompt appears.
#
# Pickers filter as text is typed, so the tape names the row it wants instead of counting Down
# presses: the preset list, the discovered model list and the credential menu all stay reachable when
# their contents change.
Output docs/media/setup-wizard.gif

Set Shell bash
Set FontFamily "Menlo"
Set FontSize 14
Set Width 620
Set Height 520
Set Padding 10
Set TypingSpeed 55ms

Type "lunate setup"
Enter
Sleep 6s

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

# The summary is the last screen: the confirmation picker is left open so the GIF ends on the
# collected values, before anything is written.
Sleep 5s
