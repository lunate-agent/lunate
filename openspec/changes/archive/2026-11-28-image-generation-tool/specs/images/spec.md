# Images Specification Delta

## MODIFIED Requirements

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
