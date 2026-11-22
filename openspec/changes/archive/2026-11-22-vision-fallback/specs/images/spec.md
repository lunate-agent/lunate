## MODIFIED Requirements

### Requirement: Vision gating

An image SHALL be sent only when the active chat model declares image input. When the chat model's known capabilities say it accepts no images and the request carries at least one image, the harness SHALL describe each image through the configured vision profile before sending the chat request: the `fast` usage when its resolved model declares image input. A user submission SHALL NOT be refused by the early image resolver when that vision profile exists. With no such profile, a user submission with an image SHALL be refused before the run with an error naming the model id and saying that the model does not support images; a `read` of an image SHALL return that same refusal as an error result. No chat request SHALL carry image content for a model without image input, and the image SHALL NOT be silently dropped.

The description SHALL be sent to the chat model as text together with the image's exact `[image <width>x<height>]` placeholder. The transcript SHALL show the description and the profile that produced it. When the vision profile fails, the turn SHALL fail with an error naming both the chat model's image refusal and the vision profile's error.

#### Scenario: A text-only model refuses the submission

- **GIVEN** a chat model whose known capabilities have no image input and no configured vision profile
- **WHEN** the user submits `@diagram.png`
- **THEN** no run starts, the notice names the chat model id, and no request is sent

#### Scenario: A vision model receives the image

- **GIVEN** a chat model whose catalog entry declares `supportsVision: true`
- **WHEN** the user submits `@diagram.png`
- **THEN** the request carries one image content part and no vision description runs

#### Scenario: A text-only chat model triggers the vision profile

- **GIVEN** a chat model whose known capabilities have no image input and a `fast` profile whose model declares image input
- **WHEN** the user submits `@diagram.png`
- **THEN** the vision profile receives the image, the chat request carries no image content, and the run starts

#### Scenario: The fast profile is the configured vision profile

- **GIVEN** two configured profiles whose models declare image input, with one bound to `usages.fast`
- **WHEN** a text-only chat model receives an image
- **THEN** only the `usages.fast` profile is called to describe it

#### Scenario: The chat model receives description and placeholder

- **GIVEN** a text-only chat model and a vision profile that returns `A red square.`
- **WHEN** the user submits `what is this? @diagram.png` with a 2x3 image
- **THEN** the chat request carries text containing `A red square.` and `[image 2x3]`, and no image content

#### Scenario: The transcript shows the description and profile

- **GIVEN** a text-only chat model and a vision profile that returns `A red square.`
- **WHEN** the fallback describes the image
- **THEN** the transcript shows the description and the profile that produced it

#### Scenario: A vision-profile failure names both errors

- **GIVEN** a text-only chat model and a vision profile whose request fails
- **WHEN** the user submits an image
- **THEN** the turn fails with an error naming the chat model's image refusal and the vision profile's error, and no chat request is sent
