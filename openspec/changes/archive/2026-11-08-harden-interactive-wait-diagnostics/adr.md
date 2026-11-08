# ADR review — harden-interactive-wait-diagnostics

ADR review completed for this change.

**In-force ADR context reviewed:** 0005 budget calibration and its addendum (interference only ever
adds time; a ceiling is sized by the condition it waits for and a failure is first asked which
condition never became true — this change is the instrument that answers that question), 0011 repo
quality tooling (CSharpier, the documentation map), 0024 bash guard and the process-isolation contract #621 established
for detached tool children (`setsid`, stdin from `/dev/null`, the guarded `posix_spawn` recipe), and
ADR-0003 (our own loop runs tools, which is why a child whose stdout never reaches EOF stalls the
loop's caller).

**No new repository-level ADR file was created.** The rule this change lands is a test-support
behaviour contract, not an architectural decision: it adds no dependency, no public API, no layer
change and no product machinery — the diagnosis reads `ThreadPool` counters, the existing
`InteractiveSession` properties and the process table, all from code that already exists in the test
project, and it is reversible by deleting one file and one optional parameter.

The rule belongs where it now lives: the `test-wait-diagnostics` capability under
`openspec/specs/test-wait-diagnostics/spec.md`. The two invariants worth carrying forward are stated
there rather than in an ADR: a timeout must name the condition and the observation, and collecting
that evidence must be bounded so it can never replace one silent failure with another.

**Deliberately not decided here:** the fix for the file-descriptor inheritance measured while
investigating #660 (a concurrent spawn inherits another child's pipe write ends). That changes the
POSIX spawn contract #621 pinned, so it needs maintainer sign-off and its own change; it is filed as
#699 and referenced from the proposal, the design and the PR. This change must not be read as
endorsing the current inheritability choice, and #699's fix will need its own ADR if it changes the
spawn flag set.
