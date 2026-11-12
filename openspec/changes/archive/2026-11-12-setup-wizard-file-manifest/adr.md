# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

The change is a rendering change inside the setup wizard: the pre-write summary becomes a manifest
grouped by the file each staged value will reach, and a short receipt follows the commit. It adds
no settings key, store writer, public API, credential path or terminal primitive. The file mapping
is the commit's own staged state, so the manifest cannot invent a write the store writers do not
make. The 40-column layout reuses the existing scrollback renderer and `CellText.Wrap`; it is a
spec-level interaction rule, not a durable architectural commitment.

## In-Force ADRs Reviewed

- ADR-0002 (layering): the renderer stays in `Lunate.Coding` and prints through `Lunate.Tui`'s
  existing scrollback path; no new layer crossing.
- ADR-0007 (live-area concurrency): the summary and receipt use the existing `WriteScrollback`
  path, the same commit boundary the header and `/help` use.
- ADR-0022 (model catalog and provider auth): credential references and the auth store are
  unchanged; the manifest only names them.
- ADR-0027 (provider store, model profiles and usages): the manifest mirrors this writer split —
  `providers.json` for provider entries, `settings.json` for profiles/usages/decisions,
  `auth.json` for a typed key.
- ADR-0029 (provider API families): the custom entry's family/placement facts are rendered, not
  changed.

## Conclusion

No durable architectural decision is introduced. The file names, the unchanged block and the
phone-width rule belong to the `setup-wizard` capability and are recorded in its spec delta.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
