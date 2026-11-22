## MODIFIED Requirements

### Requirement: Image parts in model requests

The harness SHALL store every image attachment through the session's image store and SHALL append a session message whose image parts are `ImageContentReference` values (digest, mime, width, height), never image bytes. Before each model request it SHALL hydrate those references into Microsoft.Extensions.AI `DataContent` parts for the provider, reading the blob from the session store or the in-memory store of a no-session run, when the active chat model declares image input. A tool result carrying `Images` SHALL append its tool-role message with the placeholder text and then one user-role message carrying the image contents with the text `Image from <tool>: [image <w>x<h>].`; the follow-up user message is the portable carrier because the OpenAI chat-completions adapter maps image content only in user-role messages, not in tool-role results. A resumed session SHALL hydrate the same way.

When the chat model's known capabilities say it accepts no images, the harness SHALL instead describe each referenced image through the `fast` usage's profile when that profile's model declares image input, and the request copy SHALL replace the reference with text carrying the description and the image's exact `[image <w>x<h>]` placeholder. It SHALL call the vision profile once per distinct image digest per harness and reuse the description on later requests. A vision-profile failure SHALL fail the run before the chat request, naming both the chat model's image refusal and the vision profile's error. With no vision-capable `fast` profile the harness SHALL fail the run before the request, naming the chat model id and the number of images in the request, and SHALL NOT drop them. A missing referenced blob SHALL fail the run before the first request, naming the digest and the path; the request SHALL NOT silently drop the image.

#### Scenario: A read image becomes a tool message and a user image message

- **GIVEN** a `read` call returning `[image 1024x768]` and PNG bytes
- **WHEN** the loop appends the result
- **THEN** the session gains the tool message with the placeholder and one user message whose contents carry the image reference, and the next request carries `DataContent`

#### Scenario: Resume rehydrates the image

- **GIVEN** a schema 3 session referencing a stored blob
- **WHEN** the session is resumed and a request is built on a vision model
- **THEN** the request carries the image bytes as `DataContent` and the session file still carries only the reference

#### Scenario: Resume on a text-only model fails

- **GIVEN** a schema 3 session referencing a stored blob, a model without image input, and no vision-capable `fast` profile
- **WHEN** the session is resumed and a request is built
- **THEN** the run fails naming the model id and the image count before any request, and no request carries the image

#### Scenario: Resume on a text-only model with a vision profile replaces the image

- **GIVEN** a schema 3 session referencing a stored blob, a model without image input, and a `fast` profile that declares image input
- **WHEN** the session is resumed and a request is built
- **THEN** the vision profile receives the bytes and the chat request carries its description and the placeholder, not the image

#### Scenario: A missing blob stops the run

- **GIVEN** a session referencing a digest whose blob is missing
- **WHEN** the harness is built for the run
- **THEN** it fails naming the digest and expected path before any request

#### Scenario: A no-session run keeps images in memory

- **GIVEN** a `--no-session` run with an attached image
- **WHEN** the request is built on a vision model
- **THEN** the image reaches the provider from memory and no file is written

#### Scenario: A vision-profile failure stops before the chat request

- **GIVEN** a text-only chat model, a `fast` vision profile, and a vision request that fails
- **WHEN** the request is built
- **THEN** the run fails naming both errors and the chat model receives no request
