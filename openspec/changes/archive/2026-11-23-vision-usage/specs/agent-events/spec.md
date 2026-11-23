## ADDED Requirements

### Requirement: Image description event

The event set SHALL include an `ImageDescribed` core event announcing that a vision profile replaced
a request image for a chat model that cannot see it. The event SHALL carry the run id, the image's
exact `[image <width>x<height>]` placeholder, the description, the profile name that produced it and
the resolved model id. It SHALL be a member of `AgentEvent` (not `ExtensionEvent`), SHALL carry the
same stamped session, parent-run and source fields as every other event, and SHALL be emitted once
per description before the chat request that carries it. It SHALL project to the `--json` and wire
kind `image_described` with the fields `placeholder`, `description`, `profile` and `modelId`.
Consumers that do not know the event SHALL ignore it through their declared fallback arm, as the
closed-set rule already requires. Adding the case SHALL move the contract API version and carry an
extension-author version note.

#### Scenario: The event names what replaced the image

- **GIVEN** a text-only chat model and a vision profile named `sight` whose model returns `A red square.` for a 2x3 image
- **WHEN** the fallback describes the image
- **THEN** one `ImageDescribed` event carries `[image 2x3]`, `A red square.`, `sight` and its model id

#### Scenario: The event folds into any run's stream

- **GIVEN** an `ImageDescribed` record
- **WHEN** it is emitted on a session-attached run's stream
- **THEN** it arrives as an `AgentEvent` carrying its run id and the stamped session id, and it is not an `ExtensionEvent`

#### Scenario: Consumers without an image view keep working

- **GIVEN** a consumer whose event switch handles a subset of the declared kinds
- **WHEN** an `ImageDescribed` event arrives
- **THEN** its declared fallback arm ignores the event and the stream continues
