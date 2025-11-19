# Lessons Learned – CS1591 Cleanup and Documentation Pass

- Removing CS1591 suppressions early keeps missing XML docs visible and prevents regressions.
- Meaningful summaries, params, and returns reduce rework; avoid copy/paste names.
- Document test suites with intent and behavior, not just method signatures.
- Track progress in one place (DocumentationTestTask.md) to avoid double-work.
- Run builds with warnings as errors after edits to expose dependency issues unrelated to docs.
- Leave dependency issues (e.g., OpenTelemetry package gaps) noted when out of scope so they can be fixed separately.

## Outstanding build blockers (non-CS1591)
- OpenTelemetry package resolution/vulnerability errors for UI and dependent test projects (NU1603/NU1101/NU1902) prevent a clean build until package sources/versions are adjusted.
