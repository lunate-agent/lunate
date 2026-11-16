## ADDED Requirements

### Requirement: The extension-facing packages keep a net10.0 asset

`Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent` (with the `Lunate.Ai` and
`Lunate.Ai.Decisions` assemblies its package carries) and `Mermaid.Terminal` SHALL publish a
`net11.0` asset and a `net10.0` asset. Both assets SHALL carry the same public API, so no union, no
closed hierarchy and no other construct that needs the `net11.0` base class library SHALL appear on
their public surface while `net10.0` is a target. The extension template SHALL build both frameworks,
so the floor is exercised by the build on every gate run.

#### Scenario: An extension built against the previous LTS loads in a net11 host

- **GIVEN** an extension compiled against the contract packages' `net10.0` asset
- **WHEN** a `net11.0` host loads and runs it
- **THEN** it loads through the real loader and its tool call completes, because the host binds it to
  its own contract assemblies

#### Scenario: The template proves the floor on every build

- **GIVEN** `templates/extension/TemplateExtension`
- **WHEN** the solution builds
- **THEN** the template compiles for `net10.0` and for `net11.0`

#### Scenario: The two assets carry one API

- **GIVEN** a contract package built for both frameworks
- **WHEN** the build runs the public API analyzer over both
- **THEN** the shared public API files describe both, and a type that exists in only one asset fails
  the build
