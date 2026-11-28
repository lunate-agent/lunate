# Agent Tools Specification Delta

## ADDED Requirements

### Requirement: Image generation tool

`generate_image` SHALL be registered beside the core tools whenever an image-generation profile
resolves through `usages.image`. Without such a profile the tool SHALL NOT be registered, so the
model is never offered a tool that cannot run, and one notice SHALL name `usages.image`. It SHALL
take a required non-empty `prompt` string and an optional workspace `path` string, and it SHALL make
one image-generation call through the resolved profile's client.

A successful call SHALL hand the returned bytes to the loop's existing tool-image path, so the
session sidecar stores them content-addressed (ADR-0018) and the transcript records one typed
`image` part. The model-facing result text SHALL start with the exact `[image <width>x<height>]`
placeholder and SHALL NOT contain image bytes; the placeholder's numbers SHALL be culture-invariant.
A successful call with a `path` SHALL additionally write the same bytes into the workspace and its
result SHALL name the relative path it wrote.

The returned bytes SHALL be accepted only when their magic bytes identify PNG, JPEG, GIF or WebP and
they stay within the image limits of 5 MB and 4096 pixels on either side. A refusal SHALL name the
actual value, the limit and the next step, in the same words the image input path uses.

With a `path`, the tool SHALL resolve it under the write tool's path rules: a relative path resolves
against the workspace, an absolute path inside the workspace is allowed, and a path outside it is
refused naming the requested path, the resolved target and the allowed roots before any generation
call is made. A directory target SHALL be refused, missing parent directories SHALL be created, and
an existing file SHALL be replaced. The write SHALL notify the run's file-change sink and SHALL be
covered by the write risk, so it needs the same approval a `write` call needs. A failed workspace
write SHALL be reported as an error naming the path and the reason; the generated image SHALL remain
attached to the conversation rather than silently dropped.

A provider failure SHALL be an error result naming the prompt, the profile name, the resolved model
id and the next step, and SHALL attach no image. The generation call's usage SHALL be charged to the
run as usage `image` with its profile, provider and resolved model id.

#### Scenario: A generated image is attached and stored

- **GIVEN** a stub generator returning a 640×480 PNG and an active session
- **WHEN** `generate_image` runs with a prompt and no path
- **THEN** the result text is `[image 640x480]`, the session message carries one typed image part
  whose digest is the SHA-256 of the returned bytes, and that digest names the session sidecar blob

#### Scenario: No usage means no tool

- **GIVEN** no resolvable `usages.image` profile
- **WHEN** the harness is built
- **THEN** `generate_image` is not registered and one notice names `usages.image`

#### Scenario: A path saves the bytes in the workspace

- **GIVEN** a resolved image profile and `path: "images/hero.png"`
- **WHEN** the approved call succeeds
- **THEN** `images/hero.png` exists with the returned bytes, the result names that relative path, and
  the run's file-change sink reports one created file

#### Scenario: A path outside the workspace is refused before generating

- **GIVEN** a `path` that resolves outside the workspace
- **WHEN** `generate_image` is called
- **THEN** it is refused naming the requested path, the resolved target and the allowed roots, and no
  image-generation call is made

#### Scenario: The workspace write needs the write risk's approval

- **GIVEN** the `ask` approval policy
- **WHEN** `generate_image` is offered for approval
- **THEN** it is declined exactly as `write` is, and `auto-edit` allows it

#### Scenario: A provider failure names the profile and model

- **GIVEN** a stub generator that fails
- **WHEN** `generate_image` runs
- **THEN** the result is an error naming the profile, the resolved model id and the next step, and it
  carries no image

#### Scenario: Bytes outside the image limits are refused

- **GIVEN** a stub generator returning 6 MB of PNG bytes
- **WHEN** `generate_image` runs
- **THEN** the error names `6.0 MB`, `5.0 MB` and the next step, and no image part is attached
