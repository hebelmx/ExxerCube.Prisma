# Mission 7 — Adaptive DOCX Extraction & Field Merging

Context: Implement and prove adaptive DOCX extraction using multiple strategies and merge logic. Contracts are defined by the interfaces `IAdaptiveDocxExtractor` and `IFieldMergeStrategy` plus their contract tests. References: `docs/Legal/LegalRequirements_Summary.md`, `docs/SYSTEM_FLOW_DIAGRAM.md`.

Objective: Deliver adaptive extraction that picks the best strategy or merges multiple outputs, complements existing data, and surfaces conflicts/telemetry. Ensure implementation passes contract tests and a system test with real DOCX fixtures.

Scope (3–4 services): Adaptive DOCX strategies → Adaptive extractor orchestrator → Field merge strategy → Storage/Audit/Telemetry.

Fixtures: DOCX variants (structured, semi-structured, table-heavy) + paired XML/PDF where available under `Prisma/Fixtures/DocxAdaptive/` (add variants as needed).

Test contract:
- `IAdaptiveDocxExtractor` behaviors per contract tests: default BestStrategy; null when no strategy; MergeAll dedupes collections; Complement fills gaps without overwriting; respects cancellation.
- `GetStrategyConfidencesAsync` returns sorted confidences (desc) or empty; used to pick highest-confidence strategy.
- `IFieldMergeStrategy` merges without null returns; preserves non-conflicting data; reports conflicts and merged field names; handles null entries gracefully.
- System test: Run adaptive extraction on multiple DOCX variants; assert merged fields cover mandatory fields (`MandatoryFields_CNBV`) or are flagged; audit/metrics capture chosen strategies, merge policy, conflicts.

Deliverables:
- Implementation of `IAdaptiveDocxExtractor` and `IFieldMergeStrategy` (and strategies) passing existing contract tests.
- System test chaining strategies + merge + storage/audit/metrics for DOCX fixtures.
- Razor demo page showing strategy confidences, chosen mode (best/merge/complement), merged fields, and conflicts.

Stories (parallelizable):
- Story A (Strategy implementations): Add concrete `IAdaptiveDocxStrategy` variants (structured, contextual, table); unit tests for each strategy’s extraction outputs.
- Story B (Extractor orchestrator): Implement `IAdaptiveDocxExtractor` honoring modes and confidences; satisfy contract tests; add unit tests for edge cases.
- Story C (Merge strategy): Implement `IFieldMergeStrategy` (e.g., longest/first-wins + conflict reporting); unit tests for merge policies and conflict reporting.
- Story D (System test): Compose strategies + merge + storage/audit/metrics on real DOCX fixtures; assert mandatory coverage/flags and audit telemetry.
- Story E (Demo page): Razor page to select a DOCX fixture, show strategy confidences, run best/merge/complement, display merged fields/conflicts.

Conventions (applies to this mission; do not skip):
- Solution/projects: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln`; code in Domain/Application/Infrastructure.*; UI pages under `03-UI/UI/ExxerCube.Prisma.Web.UI/Components/Pages`; system tests in `Tests.System.*`, unit tests in `Tests.Application` or relevant `Tests.*`.
- Key interfaces/classes: `IAdaptiveDocxExtractor`, `IAdaptiveDocxStrategy` implementations, `IFieldMergeStrategy`, storage repo, audit logger, metrics service.
- Code style: C# 10, warnings-as-errors; SmartEnums need EF converters; PascalCase types/properties, camelCase locals/params; keep methods small/pure; avoid solution churn.
- Fixtures: place DOCX variants under `Prisma/Fixtures/DocxAdaptive/` with clear naming; include paired XML/PDF if available for comparison.
- Telemetry: audit strategy choice/merge mode/conflicts; metrics for confidences and merge outcomes; consistent correlation ids.
- Observability & security: structured logging with correlation ids, performance timings, metrics for success/error/conflicts; audit strategy/merge events; honor authentication/authorization where applicable.

Definition of Done:
- Build passes with warnings-as-errors, no new suppressions.
- Contract tests for `IAdaptiveDocxExtractor` pass; merge strategy unit tests pass; system test passes; assertions are behavioral and stable.
- Razor demo page works end-to-end for DOCX fixtures.
- Playwright/demo headed run confirms the adaptive extractor demo (no UI assertions).
