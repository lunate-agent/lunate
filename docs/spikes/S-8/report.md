# S-8 report — one setup interaction model, compared across four agents

- Date: 2026-11-08
- Card: #680 (setup wizard improvements, 0.4.0)
- Question: how should Lunate's setup wizard behave when compared with OpenCode, `openspec init`, Pi and Gemini CLI, and which behaviour should each follow-on issue implement?
- Method: read the installed CLI help, shipped source and documentation for each tool, ran one real `openspec init`, and read the current wizard, its spec and its width tests. No provider request was made and no credential was entered; OAuth flows were read, not run. Raw notes and commands are in [`evidence/research-setup-flows.txt`](evidence/research-setup-flows.txt).
- Outcome: **adopt one model — one wizard, one write gate, three verbs, menus that return, and a review that is a file manifest.** The model is set out below; each comparison point ends in the issue that implements it; three gaps the existing issues did not cover are filed as #692–#694.

## The interaction model we adopt

### One wizard, one gate

Every interactive setup path is the same state machine: the first run, `/setup`, `lunate setup` on a terminal, and every scoped entry. Esc at a question ends the run with nothing written; the summary is the only write gate. This is already the spec's shape (`openspec/specs/setup-wizard/spec.md:31-46` and `:157-171`) and none of the four comparison tools does it better: OpenCode, Pi and Gemini persist a credential as soon as it authenticates, and `openspec init` writes as it goes. We keep the atomic gate.

### A visible path, not a wall of steps

The ten steps stay, but the header shows the stage and the position:

```text
setup — Provider › Model › Profile › Review      2/4
```

The four stages group the existing steps without hiding them: **Provider** (provider, credential, connection test), **Model** (model, model check), **Profile** (chat, fast, decision, decisions settings), **Review** (summary, write). The stage name is information, not a second navigation layer: the user still walks one step at a time.

### Three verbs everywhere

| Verb | In a question | In a menu |
| --- | --- | --- |
| Enter | confirm the highlighted answer and advance | run the highlighted action, then return to the menu |
| Back (`b`, Left) | return to the previous step with every answer kept | return to the step that opened the menu |
| Esc | leave the wizard; nothing is written | leave the menu for its parent; at the top menu, leave the wizard |

Forward (`f`, Right) is allowed only through steps that are already answered: it re-shows an answer without re-asking it. A step the walk has not reached cannot be skipped into. This makes the walk safely reversible and keeps the current go-back-on-failure rule as one use of the same verb, not a special case.

### Menus return to themselves

A menu choice that changes something returns to the same menu with the change visible; only an explicit **Done** row leaves. This is the one behaviour we generalise from OpenCode's `/connect` account list ("Run `/connect` again to add another account … activate, rename, or delete a saved account") and from the existing decisions-settings list. It applies to the provider list, the decisions-settings menu, and the configured-install entry menu. The existing one-shot decisions menu (`src/Lunate.Coding/InteractiveSession.SetupWizard.cs:1190-1220`) is the first thing folded into it.

### Selection grammar

- **Single-select** is the default: a moving marker, Enter confirms, the selected row is the answer.
- **Multi-select** is used only where the domain is plural — for example routing profiles, or "which switches do you want to change" in the decisions menu. Space toggles `[x]`, Enter confirms, and a count line ("2 selected") is visible while toggling.
- **Filtering** appears when a list has more than 15 rows: type to filter, the match count is shown, and a no-match state names the escape hatch ("No match — clear the search, or choose Custom…"). `openspec init` does exactly this for tools, including a named universal entry when the search is empty; we adopt the pattern for providers and models, not a separate search screen.
- A menu action and a step answer never share a list: menus end in **Done**, questions end in an answer.

### Quick and detailed are the same spine

- **Quick** is the default: provider, credential, model, chat profile. The connection test and model check run, fast and decision profiles default to skip, and the decisions layer keeps its off/unset defaults.
- **Detailed** walks every setting the spec lists, including the ones quick leaves alone: custom endpoint fields, thinking level, fast and decision profiles, every decisions switch, and routing descriptions.
- The mode is chosen on the first step and can be switched there; switching keeps every answer already collected rather than restarting.
- Quick never hides a default. Its review has a **Left at default** block naming each setting it did not ask about and how to change it later. That is the difference between a fast path and a silent one.

### Providers and usages are lists

- The provider step is a list with **Add another provider…** and **Done**, not a single choice. Each added provider owns its credential; a later provider may reuse an existing `env:`/`auth:` reference or store its own key. Names are unique and a duplicate is refused with a rename prompt.
- Every model picker lists `provider/model` across every configured provider, not only the provider chosen in this run. The fast and decision steps add **Set up another provider inline…**, which pushes the provider/credential/connection/model sub-walk and returns to the usage step without losing the outer answer.
- The chat profile picker follows the same rule on a configured install: a re-run can move the chat usage to any configured provider, not only the one the prefill came from.

### Review is a manifest; the receipt is short

The pre-write summary is grouped by the file it will touch, not by the order of the steps:

```text
providers.json  work-anthropic (anthropic, eu)
                personal-openai (openai, us)
auth.json       work-anthropic  auth:work-anthropic
                personal-openai env:OPENAI_API_KEY
settings.json   profile chat-main   personal-openai/gpt-4.1, thinking medium
                usage chat          chat-main
                decisions.approvals off (default)
Left at default: fast usage, decision profile, routing, questions, decide tool
Change later: /setup · /settings · lunate auth login · ~/.lunate/
```

No value line carries a secret. After confirmation, a one-screen receipt says what landed (one line per file plus the change-later commands) and returns to the session. The full manifest is the confirm gate; the receipt is the record. This resolves the apparent conflict between the spec's "nothing written before the summary confirms" and #685's "after the write, show a summary".

At 40 columns, each item is either one line or a label line followed by a value wrapped at word boundaries and indented two spaces. There are no tables or two-column rows; a list longer than the screen pages with Enter and shows the page number in the header. The 40-column render is a fixture, not an eyeball check.

### Help is in place, not in a manual

Each credential prompt says where the key comes from (the provider console for a preset, the gateway's own documentation for a custom endpoint). Each failed check states the next action and the verb that gets there. This is the one concrete thing Gemini CLI does better: its API-key dialog says the key is stored securely and links where to get one (`interactiveCli-WWICCK5B.js:15392-15412`). We adopt the line and the link text, not the keychain.

## What each comparison changes

| Point | What the others show | Verdict | What we do (issue) |
| --- | --- | --- | --- |
| First screen | `openspec init` states what it will configure before asking; Gemini's first screen is the auth dialog; OpenCode and Pi drop into the session and use `/connect` or `/login`. | Adopt the statement; keep the in-session wizard. | Header states the four stages; the first question is still the provider. #681 |
| Provider and method | OpenCode picks an integration, then an API-key/OAuth/command/environment method, and renders provider forms; Pi does the same per provider; Gemini starts with the auth method. | Adopt the two-level choice; OAuth is out by ADR-0022. | Provider first, then the credential source; custom fields stay on the provider step. #681, #693 |
| Credential | Gemini links where to get a key and says how it is stored; Pi accepts a literal, an env var or a `!command`; OpenCode lists environment connections. | Adopt the help line; keep env-first and masked entry; reject command refs for now. | Credential step names the console/docs and the next action. #693 |
| Validation | OpenCode, Pi and Gemini find a bad key or model at the first real request; `openspec init` has no check. | **We are ahead.** | Keep the connection test and model check; the summary states passed/skipped/failed. No new issue. |
| Model choice | OpenCode `/models` and Pi `/model` list models across connected providers; Gemini chooses at the prompt. | Adopt the flat list. | Every usage picker lists `provider/model` across configured providers; inline new provider. #684, #694 |
| Several providers | OpenCode `/connect` again adds an account; its account list activates, renames and deletes; newest is active. Pi stores several provider credentials. | Adopt the repeatable add; add management. | Provider list with Add another/Done (#683); manage/rename/remove/switch/rotate (#692). |
| Re-run | OpenCode reopens `/connect`; Pi uses `/login` and `/logout`; Gemini uses `/auth`; `openspec init` refreshes configured tools, pre-selected. | **Our scoped entry menu is ahead.** | Keep `/setup`'s prefilled entries; add the Done loop and the manage entry. #681, #692 |
| Back and forward | Every tool lets you cancel a prompt back to the session; `openspec init` is one screen, so no step-back exists. | Adopt general back/forward. | `b`/Left back with answers kept; `f`/Right forward only through answered steps; Esc menu-local then run-local. #681 |
| Multi-select and filter | `openspec init` has a searchable multi-select with pre-selected detected tools, a no-match hint and at-least-one validation; Gemini uses a radio list. | Adopt the grammar. | Space toggles, Enter confirms, count visible; filter over 15 rows; multi-select only for plural answers. #681 |
| Defaults and modes | `openspec init` pre-selects what it detects and names what it will configure; Gemini pre-selects the remembered auth type; OpenCode's connect/model split is effectively a fast path. | Adopt quick/detailed on one spine. | Quick names every default in the review; detailed asks every setting. #682 |
| Summary | `openspec init` reports Created/Refreshed, counts, directory, config path, next command, optional workflows and a restart hint; the other three have no write manifest. | **Our value summary is ahead; our file view is behind.** | Pre-write manifest grouped by file; post-write receipt; change-later commands. #685 |
| Abort semantics | OpenCode, Pi and Gemini keep a completed login after an abort; `openspec init` writes as it goes. | **We are ahead.** | Keep the staged, one-pass commit and the byte-identical abort. No new issue. |
| Non-interactive | OpenCode has the same methods with `--method`/`--answer`; `openspec init` has `--tools` (all, none or a list) and a detected-tool fallback; Pi reads env vars and auth files. | **We are at parity or ahead.** | Keep `lunate setup` flags and exit 2 for a missing answer; no silent fallback. #682 stays interactive-only. |
| Width | `openspec init` falls back to a static screen below 60 columns and honours reduced motion; the TUI agents use their normal layout. | Adopt the phone-width rule for the new surfaces. | The manifest and receipt render at 40 columns; one fact per line; no tables. #685 |
| Storage | OpenCode uses SQLite; Pi uses owner-only `auth.json` and optional command refs; Gemini uses the OS keychain and OAuth. | Keep AuthStore. | OAuth, keychain and command refs would each be a security decision; no change here, see below. |

## Where we are already ahead

These are not changed by this spike; they are the parts the comparison confirmed.

- **Atomic staging.** One confirm gate, byte-identical files on abort (`openspec/specs/setup-wizard/spec.md:157-171`). No comparison tool offers this.
- **Real checks before the first session.** One discovery request and one minimal model request, with cost stated, skippable and marked unverified.
- **Environment key first.** A set `OPENAI_API_KEY`-style variable is offered before typing, and stored as `env:<VAR>`.
- **Scoped re-runs.** `/setup` edits one thing with current values prefilled, instead of walking the wall again.
- **No silent non-interactive defaults.** `lunate setup` invents nothing; a missing answer exits 2 with its name.
- **Explicit provider shape.** Region, API family, credential placement and models URL are visible and written, not hidden in a preset.
- **Narrow-width fixtures.** The existing walk is pinned at 80 and 60 columns; the new manifest adds 40.

## Deliberately not adopted

- **Subscription OAuth.** OpenCode, Pi and Gemini all offer it; ADR-0022 §4 explicitly rejects it because those plans authorize first-party clients. Not a setup-flow gap.
- **OS keychain storage.** Gemini's keychain is a better storage story than a file, but changing the credential store is a security boundary and needs its own decision; AuthStore stays owner-only.
- **A `!command` credential reference.** Pi supports it; adding process execution to credential resolution is a security decision, not a wizard refinement. Raise it separately if wanted.
- **Write-as-you-go.** It keeps a credential when the user aborts, but it breaks the one-gate invariant the spec requires and the summary depends on. We keep the gate.
- **Auto-configured tools or providers without confirmation.** `openspec init` pre-selects detected tools and still asks; we prefill and still ask. No silent write.
- **Animation.** `openspec init` has a reduced-motion-aware welcome; Lunate's setup has no animation and needs none. A static header is the accessible default.

## Refined issue list

The five issues keep their scope; their bodies now carry the interaction decisions above. Three gaps are new.

### #681 — navigation: back/forward, menus that return, multi-select

- Add the visible four-stage path and the three verbs (`Enter`, `b`/Left, Esc) with the menu-local/run-local split.
- Add forward replay through answered steps only.
- Generalise the decisions-settings menu into the return-to-menu-with-Done shape; the open #687 keeps that list open, so #681 generalises its shape rather than duplicating it.
- Multi-select only for plural answers (routing profiles, a multi-switch edit); single-select everywhere else; filter over 15 rows.
- Acceptance additions: back from the summary changes two answers and comes forward without re-asking; a menu action returns to the same menu and shows the change; a three-item multi-select leaves a visible count; the tests drive key sequences, not labels.

### #682 — quick and detailed modes

- Define both on the same step list, with quick as the first-step default and a mode switch that preserves answers.
- Quick covers provider, credential, model and chat profile; detailed covers every remaining setting in spec steps 1–8.
- The quick review names every default it did not ask about under **Left at default**, with the command that changes it.
- Acceptance additions: the spec lists both setting sets; a test asserts quick's review names every default it skipped; a detailed walk reaches every setting quick hides.

### #683 — several providers in one run

- The provider step becomes a list with **Add another provider…** and **Done**; each add returns to the list.
- Each provider owns a credential; a later provider may reuse an existing `env:`/`auth:` reference or store its own key. A duplicate name is refused with a rename prompt.
- The review groups providers, and their credentials, under their files; the commit still writes all of them in one pass.
- Acceptance additions: two providers in one run, both in the written home and the review; a test asserts the config, not the summary text; a duplicate-name and a credential-reuse case are covered.

### #684 — fast and decision usages from any provider

- Every usage picker is a flat `provider/model` list across configured providers, with family, native/adapter label and thinking where relevant.
- **Set up another provider inline…** runs the provider/credential/connection/model sub-walk and returns to the same usage step with the outer answer intact.
- The check for the selected model runs after selection; the three routes (same provider, other configured provider, inline new provider) are separate tests.
- Acceptance additions: each route reaches a passing check and writes the profile/usage; the inline route adds the provider to the same run's review and commit; no route loses the answer the step already had.

### #685 — final summary of everything written

- The pre-write manifest is grouped by file: `providers.json`, `auth.json`, `settings.json`; every provider, profile, usage and decision setting names its file.
- A **Left at default / unchanged** block names what the mode did not ask, with how to change it later (`/setup`, `/settings`, `lunate auth login`, the file path).
- The post-write receipt is the short form: one line per file, the change-later commands, and a return to the session.
- The 40-column rule: one fact per line, label-then-indented-value, no tables, paged lists, no secret values; reuse #689's real narrow-width harness rather than adding a second width rig.
- Acceptance additions: both surfaces render and are checked at 40 columns; a test maps every staged write to its file; a secret never appears.

### #692 — manage configured providers and credentials (new)

The comparison's clearest missing capability. `openspec init` refreshes configured tools, but OpenCode's account list (activate, rename, delete) and Pi's `/logout` have no Lunate equivalent: `/setup` can add a provider but cannot inspect, rename, remove, test or replace one.

Scope: a **Manage providers** entry in `/setup` listing each configured provider with its kind, region, credential source/status and whether the chat usage binds it; actions to rename, remove, test, replace the credential, and switch the chat binding. Rename and remove update every reference in `providers.json` and `settings.json`; removal is refused, with the referencing profiles named, when a profile or usage still uses it. Acceptance: rename and remove round-trip through the stores; a refused remove names the references; a replaced credential is written only through AuthStore; the list renders at 40 columns. Milestone 0.4.0.

### #693 — contextual help and credential acquisition (new)

Gemini's API-key dialog links where to get a key and says how it is stored; OpenCode's OAuth dialog opens or copies the authorization details; Pi names the environment variable per provider. Lunate's credential step does not yet say where the key comes from.

Scope: a provider-specific help line at the credential step (the preset's console link, or "see your gateway's docs" for a custom endpoint), plus a one-line storage statement and the next action on every check failure. Help text lives in `Lunate.Coding`; no provider-schema or public-API change. Acceptance: every preset has a help line, the custom path has one, the credential prompt never contains a secret, and the help wraps at 40 columns. Milestone 0.4.0.

### #694 — chat profile picker across configured providers (new)

The same flat-list rule #684 applies to fast and decision also applies to the chat usage on a configured install: today the re-run prefills the current provider and cannot move chat to another provider's model. OpenCode `/models` and Pi `/model` both list every connected provider.

Scope: the chat-profile step's model list spans every configured provider; choosing another provider's model rebinds the `chat` usage and writes only the changed usage/profile. The first-run path is unchanged because only one provider exists yet. Acceptance: a configured home with two providers can move `chat` from one to the other in one entry walk; a test asserts the usage and profile entries, not the summary text. Milestone 0.4.0.

## Evidence and limits

Raw evidence, exact versions, commands and source references are in [`evidence/research-setup-flows.txt`](evidence/research-setup-flows.txt). The comparison is a one-day, one-machine read of installed tools; OAuth, keychain storage and provider forms were read, not exercised. Every claim about another tool cites its installed help, shipped source or documentation at the version observed on 2026-11-08. The Lunate references are to the code at `origin/main` commit `2649040c`, the base of this spike.
