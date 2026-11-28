# images Specification

## Purpose

Images reach the model as first-class attachments: an `@<path>`, a dropped file or a clipboard grab becomes one stored, deduplicated image part, a request to a model without vision fails before it is sent, and a resumed session replays the stored bytes instead of re-reading the file.

## Requirements

### Requirement: Image references in submitted text

In interactive and print modes, a submitted text SHALL resolve image references after the input hook chain and before any run starts. A reference is a token that starts with `@` at the beginning of the text or after whitespace, with the path continuing to the next whitespace; `@"<path>"` SHALL carry a path containing spaces. A submission whose trimmed text is exactly one existing supported image path — the shape a terminal produces when a file is dropped — SHALL count as one reference. Relative paths SHALL resolve against the working directory; absolute paths SHALL be allowed. A reference to an existing supported image SHALL become one attachment and the token SHALL be replaced in the processed text with the placeholder `[image <width>x<height>]`. A reference to an existing file that is not a supported image SHALL stay in the text as typed. A token whose path has a supported image extension but does not exist SHALL refuse the submission with an error naming the path. Repeated references to the same file SHALL produce one attachment.

#### Scenario: A referenced image attaches

- **GIVEN** a workspace file `diagram.png` (1024×768) and a model with vision
- **WHEN** the user submits `explain @diagram.png`
- **THEN** the run starts with the text `explain [image 1024x768]` and the request carries the image once

#### Scenario: A quoted path carries spaces

- **GIVEN** a file `screens/login page.png`
- **WHEN** the user submits `@"screens/login page.png" what is wrong here?`
- **THEN** the image attaches and the processed text reads `[image <w>x<h>] what is wrong here?`

#### Scenario: A non-image reference stays text

- **GIVEN** a workspace file `notes.txt`
- **WHEN** the user submits `summarize @notes.txt`
- **THEN** no attachment is made and the text reaches the model unchanged

#### Scenario: A missing image path is refused

- **GIVEN** no file named `missing.png`
- **WHEN** the user submits `look at @missing.png`
- **THEN** no run starts and the error names `missing.png`

#### Scenario: A dropped path attaches

- **GIVEN** a terminal that pastes the path `/tmp/shot.png` as the whole submission
- **WHEN** the submission arrives
- **THEN** it is treated as one image reference and the user block shows `[image <w>x<h>]`

#### Scenario: Print mode resolves and refuses

- **GIVEN** `lunate -p "compare @a.png with @b.png"` and a missing `b.png`
- **WHEN** the command runs
- **THEN** it exits 2 with the error on stderr and makes no model call

### Requirement: Clipboard image input

`@clipboard` SHALL read one image from the system clipboard through the platform helper: `pngpaste -` on macOS, `wl-paste --type image/png` on Wayland, and PowerShell `Get-Clipboard -Format Image` on Windows, in that platform order. The helper output SHALL pass the same format, size and dimension checks as a file reference. When no helper is available, interactive mode SHALL show one notice per session naming the helper for the platform and SHALL start no run; print mode SHALL fail with the same text on stderr. An empty clipboard SHALL produce a notice and start no run. Temporary files created for a helper SHALL be removed after the bytes are read.

#### Scenario: A clipboard image attaches

- **GIVEN** a stub clipboard helper returning a 640×480 PNG
- **WHEN** the user submits `describe @clipboard`
- **THEN** the processed text is `describe [image 640x480]` and the request carries the image

#### Scenario: An absent helper is reported once

- **GIVEN** no clipboard helper resolves and an interactive session
- **WHEN** `@clipboard` is submitted twice
- **THEN** the first submission shows the notice naming the platform helper, the second shows no repeated notice, and neither starts a run

#### Scenario: An empty clipboard is not an image

- **GIVEN** a helper that exits successfully with no bytes
- **WHEN** `@clipboard` is submitted
- **THEN** a notice says the clipboard has no image and no run starts

### Requirement: Image validation and limits

An attachment SHALL be accepted only when its magic bytes identify PNG (`89 50 4E 47 0D 0A 1A 0A`), JPEG (`FF D8 FF`), GIF (`GIF87a` or `GIF89a`) or WebP (`RIFF....WEBP`); the file extension SHALL NOT decide. Each image SHALL be at most 5 MB, at most 4096 pixels on either side, and a message SHALL carry at most four images. Every refusal SHALL name the actual value, the limit and the next step; an oversize refusal SHALL say `downscaling is not available yet`. Formats SHALL be named as `PNG, JPEG, GIF or WebP`. Numbers SHALL be formatted culture-invariantly.

#### Scenario: Magic bytes decide the format

- **GIVEN** a PNG file named `picture.txt`
- **WHEN** it is attached
- **THEN** it is accepted as `image/png`

#### Scenario: An unsupported file is refused

- **GIVEN** an SVG file
- **WHEN** it is referenced
- **THEN** the refusal names the supported formats and starts no run

#### Scenario: The size limit is refused without downscaling

- **GIVEN** a 6 MB PNG
- **WHEN** it is referenced
- **THEN** the refusal names `6.0 MB`, `5.0 MB` and `downscaling is not available yet`

#### Scenario: The dimension limit is refused

- **GIVEN** a 5000×500 PNG
- **WHEN** it is referenced
- **THEN** the refusal names `5000` and `4096` and starts no run

#### Scenario: At most four images per message

- **GIVEN** five valid images
- **WHEN** one submission references all five
- **THEN** the refusal names `4` and starts no run

### Requirement: Placeholder contract

The text representation of an attached image SHALL be exactly `[image <width>x<height>]` with a lowercase `x` and culture-invariant integers. The placeholder SHALL appear in the processed user text, the transcript entry, the scrollback user block, a successful `read` tool result and a successful `generate_image` tool result. Image bytes SHALL NOT appear in events, print-mode JSON, session message parts or fixture files; only the content digest identifies them.

#### Scenario: The placeholder is the only text

- **GIVEN** an attached 1024×768 image
- **WHEN** the user block and the transcript entry are rendered
- **THEN** both show `[image 1024x768]` and neither contains base64 data

#### Scenario: Event JSON carries no bytes

- **GIVEN** `lunate -p --json` with an attached image
- **WHEN** the event lines are inspected
- **THEN** no event contains base64 image data and the tool result text is the placeholder

#### Scenario: A generated image names itself with the placeholder

- **GIVEN** a `generate_image` call that returned a 640×480 PNG
- **WHEN** its tool result and the transcript entry are rendered
- **THEN** both carry `[image 640x480]` and neither contains base64 image data

### Requirement: Vision gating with usages.vision

An image SHALL be sent only when the active chat model declares image input. When the chat model's known capabilities say it accepts no images and the request carries at least one image, the harness SHALL describe each image through the configured vision profile before sending the chat request: the `vision` usage when its resolved model declares image input. A user submission SHALL NOT be refused by the early image resolver when that vision profile exists. With no such profile, a user submission with an image SHALL be refused before the run with an error naming the model id and saying that the model does not support images; a `read` of an image SHALL return that same refusal as an error result. No chat request SHALL carry image content for a model without image input, and the image SHALL NOT be silently dropped.

The description SHALL be sent to the chat model as text together with the image's exact `[image <width>x<height>]` placeholder. The transcript SHALL show the description, the profile that produced it and the model id, through the dedicated `ImageDescribed` event. When the vision profile fails, the turn SHALL fail with an error naming both the chat model's image refusal and the vision profile's error.

#### Scenario: A text-only model refuses the submission

- **GIVEN** a chat model whose known capabilities have no image input and no configured vision profile
- **WHEN** the user submits `@diagram.png`
- **THEN** no run starts, the notice names the chat model id, and no request is sent

#### Scenario: A vision model receives the image

- **GIVEN** a chat model whose catalog entry declares `supportsVision: true`
- **WHEN** the user submits `@diagram.png`
- **THEN** the request carries one image content part and no vision description runs

#### Scenario: A text-only chat model triggers the vision profile

- **GIVEN** a chat model whose known capabilities have no image input and a `vision` profile whose model declares image input
- **WHEN** the user submits `@diagram.png`
- **THEN** the vision profile receives the image, the chat request carries no image content, and the run starts

#### Scenario: The vision usage is the configured vision profile

- **GIVEN** two configured profiles whose models declare image input, with one bound to `usages.vision`
- **WHEN** a text-only chat model receives an image
- **THEN** only the `usages.vision` profile is called to describe it

#### Scenario: The chat model receives description and placeholder

- **GIVEN** a text-only chat model and a vision profile that returns `A red square.`
- **WHEN** the user submits `what is this? @diagram.png` with a 2x3 image
- **THEN** the chat request carries text containing `A red square.` and `[image 2x3]`, and no image content

#### Scenario: The transcript shows the description and profile

- **GIVEN** a text-only chat model and a vision profile that returns `A red square.`
- **WHEN** the fallback describes the image
- **THEN** the transcript carries an `ImageDescribed` event showing the description, the `[image 2x3]` placeholder and the profile name that produced it

#### Scenario: A vision-profile failure names both errors

- **GIVEN** a text-only chat model and a vision profile whose request fails
- **WHEN** the user submits an image
- **THEN** the turn fails with an error naming the chat model's image refusal and the vision profile's error, and no chat request is sent
