## 1. Usage role and settings

- [x] 1.1 Change `VisionFallback.Usage` to `vision`, update its comments and keep the image-input
  requirement check unchanged.
- [x] 1.2 Accept `usages.vision` in `SettingsStore.Profiles.ReadUsages`, add the key to
  `src/Lunate.Coding/settings-schema.json`, and regenerate `docs/settings.md` with `scripts/docs-sync`.
- [x] 1.3 Keep interactive, print and ACP frontends wiring the resolved vision profile under the
  `vision` usage key (no `fast` path remains for vision).
- [x] 1.4 Update the harness usage resolution and attribution from `fast` to `vision` so a vision
  call records `usage: "vision"`.

## 2. ImageDescribed event

- [x] 2.1 Add the `ImageDescribed` record to `Lunate.Contracts`, its `AgentEvent` union case, the
  type forwarder, and the public API files.
- [x] 2.2 Emit `ImageDescribed` from the harness instead of `SteeringInjected`, carrying placeholder,
  description, profile and model id; render it in the interactive transcript and print notices, and
  add its diagnostic detail.
- [x] 2.3 Add the `image_described` kind to the remote/`--json` projection and conformance inventory,
  and the published `schemas/extensions-remote.schema.json`.
- [x] 2.4 Move `ExtensionApi.Current` and `ContractApiVersion` to 1.19.0 and add the 1.19.0
  extension-author note in `docs/extensions/README.md`; document the event in `docs/json-events.md`.

## 3. Documentation and decision record

- [x] 3.1 Replace the `fast` vision wording with `usages.vision` in
  `docs/models-and-providers.md` and `docs/providers.md`.
- [x] 3.2 Append the dated usage-role amendment to
  `adr/0027-provider-store-model-profiles-and-usages.md` and update its status line.
- [x] 3.3 Add the changelog fragment under `changes/<pr-number>.md`.

## 4. Tests

- [x] 4.1 Update `AgentVisionFallbackTests` for `vision` binding, `ImageDescribed`, the both-errors
  failure and the unchanged refusal when `usages.vision` is unset or its model has no image input.
- [x] 4.2 Add a test that `usages.fast` with a vision model no longer triggers the fallback.
- [x] 4.3 Add settings tests that `usages.vision` is accepted and attributed with usage `vision`,
  and update the event/schema/remote conformance tests for the new kind and version.
- [x] 4.4 Run the fast gate, `openspec validate vision-usage --type change --strict`, and the
  touched test classes.
