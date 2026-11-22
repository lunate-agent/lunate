## 1. Contract

- [ ] 1.1 Add `IObservable<AgentEvent> Events` to `IExtensionContext` and `Events` to
      `ExtensionManifest` (parsed, shape-validated, `*` only alone).
- [ ] 1.2 Bump `ExtensionApi.Current` and `Directory.Build.props` `ContractApiVersion` to `1.17.0`;
      update the contract version tests and the authoring contract note.
- [ ] 1.3 Add the analyzer entries to `Lunate.Extensibility.Abstractions`'s
      `PublicAPI.Unshipped.txt`.

## 2. The stream

- [ ] 2.1 Add `SessionEventStream` to `Lunate.Extensibility`: per-subscription bounded queue and
      pump, drop-oldest with a counted, once-only warning, a throwing `OnNext` dropping only that
      subscription, `OnCompleted` once after the queue, and an awaitable bounded completion.
- [ ] 2.2 Wire it through `ExtensionContext.Events`, the loader (created once, completed on unload,
      rollback and session end) and `ExtensionSession.Publish`.
- [ ] 2.3 Add the `#:event` header directive and its scanner case so the manifest mirror holds.
- [ ] 2.4 Publish from the interactive-session and print-mode run loops and from the test kit's
      `ExtensionTestHost`; warn once when a remote manifest declares `events`.

## 3. Surfaces

- [ ] 3.1 Show a loaded extension's declared watches and its subscription counters (seen, dropped)
      in `/extensions` text and `--json`, leaving a quiet extension's line unchanged.
- [ ] 3.2 Carry the declared watch list in the interactive trust question as a detail line.

## 4. Tests

- [ ] 4.1 Stream unit tests: ordered delivery, drop-oldest with one warning and the count, a throwing
      observer dropping only itself, completion once after queued events, disposal stopping delivery.
- [ ] 4.2 Manifest and header tests for `events` and `#:event`, including the wildcard-alone refusal.
- [ ] 4.3 A conformance test: an installed extension that only subscribes observes a print-mode turn
      from `RunStarted` to `RunFinished` without registering a hook.
- [ ] 4.4 `/extensions` view tests for the watches clause and the drop count.

## 5. Docs and close-out

- [ ] 5.1 Document the surface for extension authors: a contributions page, the index row, the
      manifest and single-file rows, `docs/dev/spec/extensibility.md`, and the code example.
- [ ] 5.2 Validate the change (`openspec validate extension-event-stream --strict`), format with
      CSharpier, run the named test classes and Release builds of every touched project.
- [ ] 5.3 Write `changes/<pull-request-number>.md`, sync the spec delta and archive the change as the
      last commit.
