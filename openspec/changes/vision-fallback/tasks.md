## 1. Harness fallback

- [x] 1.1 Add failing `Lunate.Agent.Tests` cases for the trigger, the fast-profile choice, the text and placeholder the chat model receives, the transcript line, and the both-errors failure.
- [x] 1.2 Implement the request-build fallback: describe each distinct image through the `fast` usage when the chat model is known not to accept images, cache by digest, and replace the reference with description plus placeholder in the request copy.
- [x] 1.3 Record the vision call's usage and emit the transcript line naming the profile; fail with both errors when the vision call fails, and keep the existing refusal when no vision-capable `fast` profile exists.

## 2. Frontend resolution

- [x] 2.1 Let the early image resolver accept a submission when a vision-capable `fast` profile is configured, in interactive and print modes, without changing the no-profile refusal.
- [x] 2.2 Resolve the `fast` usage client in print and ACP as well as the interactive session, so all frontends can run the fallback.
- [x] 2.3 Add or update coding tests for the resolver gate and the fast-profile resolution.

## 3. Documentation

- [x] 3.1 Document the `fast`-as-vision-profile policy, the request text, the transcript line and the failure in the profile, image and provider docs.
- [x] 3.2 Add the changelog fragment under `changes/1014.md`.

## 4. Verification and archive

- [ ] 4.1 Run CSharpier, the named tests, `openspec validate vision-fallback --type change --strict`, and the fast gate build.
- [ ] 4.2 Sync the delta specs, archive the change as the last commit, and run `openspec validate --all --strict`.
