# ADR review — setup-wizard-navigation

ADR review completed for this change. Every in-force ADR under `adr/` was read.

Reviewed for context (no decision changes): ADR-0003 (the loop owns tool execution),
ADR-0017 (extensibility and UI by mode), ADR-0022 (no subscription OAuth; `AuthStore` stays the
credential store), ADR-0027 (provider store, model profiles and usages), ADR-0029 (provider API
families).

**No major durable architectural decision was introduced by this change, and no repository-level
ADR file was created.** The change stays inside the existing interaction primitives: the wizard's
own state machine, the picker, the multi-select question component and the notification slot. It
adds no dependency, no public API and no wire or storage contract. The navigation model it adopts
is already recorded in the S-8 spike report (`docs/spikes/S-8/report.md`) as an interaction
decision, which is not an architecture decision record.
