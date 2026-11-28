## 1. Settings and the image usage

- [x] 1.1 Accept `usages.image` in `SettingsStore.Profiles.ReadUsages`, add the key to
  `src/Lunate.Coding/settings-schema.json`, and add its probe to `SettingsSchemaTests`.
- [x] 1.2 Add `ImageGenerationUsage` (profile resolution, the declared-image-output requirement, the
  `openai-chat` family rule, the suggested notice naming `usages.image`) with tests for the
  known-absent refusal, the unknown-set acceptance and the family refusal.

## 2. The provider seam

- [x] 2.1 Add the internal image wire to `ChatClientFactory` over the existing endpoint, header and
  credential transport, with a wire test that the request reaches the provider's
  `/images/generations` path with the declared credential placement and wire model id. MEAI's
  experimental image types stay behind the factory's one suppression and its
  `ImageGenerationWireResult`.
- [x] 2.2 Add `IImageGenerationClient`/`GeneratedImageResult` and `ProviderImageGenerationClient`,
  returning bytes, media type and the provider usage; a response without bytes (a URL-only image)
  is refused by the seam with a model-facing message.

## 3. The tool

- [x] 3.1 Add `GenerateImageTool` with `prompt` and optional `path` over the client seam: the
  placeholder result, the image limits on returned bytes, the optional workspace write under the
  write tool's path rules and risk, the file-change notification and the actionable errors. Tests:
  attachment (placeholder + one typed image part + sidecar blob), path write, path outside the
  workspace refused before generation, directory target, provider failure, oversize bytes, bad
  arguments.
- [x] 3.2 Register `generate_image` in `HarnessFactory` only when an image binding resolves; warn
  once naming `usages.image` otherwise. Tests: unset usage registers no tool, a resolved binding
  registers it.
- [x] 3.3 Wire the interactive, print and ACP frontends to resolve the binding, and skip `image` in
  the interactive chat-usage loop.

## 4. Usage attribution

- [x] 4.1 Record the generation call's usage as `image` through the existing
  `AgentToolContextState.RecordUsageFor` path with its profile, provider and model id.
- [x] 4.2 Persist attributed `image` usage in the interactive session beside decision and embeddings,
  with a test that it reaches `/cost` and the session entry.
- [x] 4.3 Confirm no session-schema change: a schema-4 session with an image usage reloads unchanged.

## 5. Documentation, ADR and changelog

- [x] 5.1 Update `docs/settings.md`, `docs/models-and-providers.md` and `docs/how-lunate-works.md`
  (and any other page that lists the tools), regenerating `docs/settings.md` with
  `scripts/docs-sync`.
- [x] 5.2 Append the dated amendment to `adr/0027-provider-store-model-profiles-and-usages.md` and
  update its status line.
- [x] 5.3 Add the changelog fragment at `changes/<pr-number>.md`.

## 6. Verification

- [x] 6.1 Run `dotnet csharpier format .`, the fast gate, `openspec validate image-generation-tool
  --type change --strict`, and the touched test classes; the golden session files stay untouched.
- [x] 6.2 Sync the delta specs, then `openspec archive image-generation-tool --yes` as the last
  commit.
