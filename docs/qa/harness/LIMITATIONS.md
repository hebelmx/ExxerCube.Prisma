# ExxerCube.Prisma QA Harness — Limitations

**Status:** Phase-1 as-built. **Date:** 2026-06-20.
**Companion docs:** [`ARCHITECTURE.md`](./ARCHITECTURE.md) · [`CAPABILITY-INVENTORY.md`](./CAPABILITY-INVENTORY.md) · [`README.md`](./README.md)

These are **documented constraints**, not defects. They scope what the harness can
and cannot exercise so the Phase-2 QA agent assigns honest dispositions
(NOT TESTED / NEEDS HUMAN REVIEW) where coverage is gated.

## Unsupported scenarios
1. **Live SIARA portal.** The harness drives only the local SIARA *simulator*. The
   real portal requires legal authorization (counsel sign-off, RC5 P7) and is out of scope.
2. **Signed-PDF export.** Owner ruling: signed PDF is P2 / out of MVP. No digital-signature validator is provided.
3. **Sentinel monitoring service.** OUT-OF-MVP (no host, not DI-registered). No hosting controller or workflow covers it.
4. **Veriqan compliance pipeline.** Has its own `CalibrationHarness`; the QA Harness does not duplicate it.

## Environment gates (capability becomes available only when the gate is met)
5. **SIARA curated corpus is ABSENT.** `Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/`
   is gitignored generated data, lost post-reboot. The simulator serves 0 cases, so
   `IngestionWorkflow`/`ExportWorkflow` and the live SIARA path **Abort with `CorpusAbsent`**
   until the corpus is regenerated. `CorpusSeeder` can regenerate via the Python generator
   or fall back to static `Prisma/Code/Fixtures/` (image/text only — satisfies presence checks,
   not multi-format ingestion).
6. **Playwright browser binary.** `LoginWorkflow`/`ManualReviewWorkflow` and screenshot
   evidence need `playwright install chromium` on the machine. Without it those workflows
   are `Skipped` (RequiredCapability "Playwright").
7. **Docker.** Container provisioning + the live integration test require Docker (29.5+).
   Absent → those capabilities report `CapabilityUnavailable` / tests are environment-gated.
8. **Three-process live proof.** `ThreeProcessHostController` verifies the 3 hosts started
   (Services non-null) but a full live 3-host pipeline run is Docker-compose/corpus-gated and
   not exercised in the self-test suite.

## Measurement / calibration constraints
9. **OCR detection power is unmeasured.** `OcrTextValidator` runs against the PRP1-Degraded
   fixtures; without a labelled real-document corpus, precision/recall are not quantified
   (same constraint as the Veriqan calibration harness).
10. **Quality-model coefficients.** The product's filter-selection/quality models use stub/GA
    coefficients trained on an unknown corpus; the harness observes outputs but cannot validate
    model accuracy without a ground-truth corpus.

## Design-scope constraints (deliberate)
11. **No quality judgments.** By design the harness emits findings + structural `IsConformant`
    + observed data only. It never decides PASS/FAIL/READY — the Phase-2 QA agent does.
12. **Sequential workflow execution.** `DefaultWorkflowRunner` runs workflows sequentially to
    avoid shared-state collisions (mirrors the MaxFidelityGate collection). Parallel execution is a future enhancement.
13. **Evidence: video + network HAR are opt-in.** Off by default (overhead); report
    capability-unavailable until enabled.
14. **Log evidence is an in-memory ILogger buffer**, not a Serilog sink (Serilog is not centrally
    pinned for this library project) — captures the same data; a deviation from ARCHITECTURE §3.5's wording.
15. **DI extension ordering.** `IWorkflowRunner` is a singleton; it resolves the registered
    `IWorkflow` set from the built provider. Call `AddWorkflow<T>()` / `AddValidator<…>()` /
    `AddReportWriter<T>()` **before** `BuildServiceProvider()` — registrations added after the
    provider is built require rebuilding the provider to appear in `runner.AvailableWorkflows`
    (standard `IServiceCollection` immutability, not a harness bug).

## Areas requiring future enhancement
- Concrete live exercise of `IngestionWorkflow`/`ExportWorkflow` once the SIARA corpus is restored.
- Browser-workflow self-tests once a Playwright browser is provisioned in CI.
- A labelled real-document corpus to calibrate OCR/quality validators.
- Optional: promote the self-test SQL containers to a shared assembly fixture if CI time requires.
- Optional: an `IReportWriter` for JUnit/CI-dashboard XML.
