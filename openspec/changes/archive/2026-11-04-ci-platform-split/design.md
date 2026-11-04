# Design: where the line between "platform" and "OS" is drawn

## The line

A check belongs to `platform` when its result is identical on every runner. That is a property of
the check, not a guess: formatting reads source, docs-sync reads source, the public-API diff reads
git history, `openspec validate` reads prose, and the scratch/spec checks read the diff and the
filesystem layout — none of them invokes the compiler against a platform-specific target. The
artifact proofs (publish, startup budget, installer, package roundtrips, memory checks) do differ
per OS and stay on the OS legs.

`verify.sh` keeps one implementation of each check (`platform_checks`) and calls it from three
places: the `platform` profile, the full profile's shard 1 when `VERIFY_SKIP_PLATFORM_CHECKS` is
unset (so a local `verify.sh` is still the whole gate), and nowhere in a CI OS leg. There is no
second copy of the format command to drift.

## Why a separate job rather than an `if:` on steps

The checks need the pinned tool set (`dotnet tool restore`, CSharpier), a `dotnet restore`, Node and
the pinned OpenSpec CLI. Spreading those preconditions across four OS legs is what made the step
list inconsistent; one job installs them once. It also gives the 31% wall-time gain its own
observation point in the run graph.

## The PowerShell path decision

`scripts/ci-powershell-paths.sh` answers one question and prints `powershell=true|false`. Deny by
default: a path runs the leg unless it is provably unrelated.

- Path patterns that always run: `scripts/**/*.ps1`, any `*.psm1`, `install.ps1`, `packaging/**`,
  `.github/workflows/**`.
- Source files run the leg when their *content* carries a Windows-specific marker. That is the
  "derive the list from the code" rule: a new file with a Windows branch is covered without anyone
  editing a list. The markers are deliberately literal (see the script); a marker that appears in
  every file would make the filter meaningless.
- Everything else — prose, Linux-only code, the contract packages — does not run it.

The decision is a separate job (`changes`) so its output can be read by both the PowerShell leg and
`ci-ok`. A job that is skipped cannot explain why; an output can.

## The aggregate gate's new rule

`ci-ok` must treat "skipped because its paths did not match" as success and every other skip as
failure. The distinction is carried by `needs.changes.outputs.powershell`, never inferred from the
skip itself. An empty decision means the `changes` job did not run or failed — red, not "probably
fine". The self-test pins all four states: skipped-when-required, skipped-when-not, ran-and-failed,
and the unreadable case.

## Nightly

The path filter is only safe if something runs the leg without it. The nightly schedule, every tag
and a manual dispatch force `powershell=true`. A red nightly files one issue (deduplicated by title)
with `kind/bug` and the 0.3 milestone, linking the run and naming the two possible causes — the code
or the filter.

## Rejected alternatives

- **Shard the PowerShell leg.** Its tests are 9.6 of 21.8 minutes; halving them lands the job at
  ~17 min, under the macOS leg's 17.5. No wall-time gain, double the Windows minutes. Revisit only
  after the platform split, with measured numbers.
- **Publish once per OS and share an artifact.** Worth ~4 min per shard-1 leg, but it is CI plumbing
  with its own failure modes (artifact size, cache staleness) and was not needed to get the first
  win. It stays on the table as the next lever.
- **Move the checks to the docs profile.** Code pull requests never run it, and the bypasses these
  checks close (#241/#313) arrived through code.
