# The setup wizard, recorded for docs/media/setup-wizard.gif.
#
# Run it through scripts/render-setup-wizard.sh and never by hand: the script starts the keyless stub,
# points LUNATE_HOME at a throw-away directory and keeps it out of the workspace (the binary refuses a
# home inside it). The recording must show the corrected masking — only the credential prompt hides
# what is typed — and a narrow walk with no repeated lines, hence ~40 columns.
#
# Timing, not `Wait`: the wizard draws a full screen, so VHS's Wait matches escape sequences rather
# than the words on screen (it reported only the last line). The margins are generous because the
# recorder pays for Chrome plus a cold .NET start before the first prompt appears.
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

Enter
Sleep 3s
Enter
Sleep 3s

# The credential step is a menu first — "enter a key", an env:/auth: reference, or skip — so the
# option has to be accepted before the key line exists. Typing straight into the menu did nothing, and
# the recording sat on an empty field: this Enter is the one that opens the masked prompt.
Sleep 4s
Enter
Sleep 4s
Type "not-a-real-key"
Sleep 1s
Enter
Sleep 4s

Enter
Sleep 4s

Enter
Sleep 3s
Enter
Sleep 3s
Enter
Sleep 3s
Enter
Sleep 4s

Sleep 3s
Type "/quit"
Enter
Sleep 2s
