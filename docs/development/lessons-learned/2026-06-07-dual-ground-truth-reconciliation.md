# Lessons Learned — Dual Ground-Truth Reconciliation (2026-06-07)

From the documentation/code reconciliation pass. See
`docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md`.

## Analysis lessons (how to read this codebase honestly)

1. **The DI composition roots are the only reliable ground truth for "what's wired."**
   Service classes existing, being real, and being unit-tested says nothing about whether
   they run in production. Three roots matter: `Web.UI/Program.cs`, `Athena.Worker/Program.cs`,
   `Orion.Worker/Program.cs`. Read the registrations before believing any status doc.

2. **An optional-dependency orchestrator hides gaps.** `ProcessingOrchestrator` takes every
   stage as nullable and *skips with a warning* when unregistered. The pipeline can "pass"
   while silently doing nothing. Always check both the registration *and* the data flow
   between stages — here Fusion is wired but fed `null,null,null`.

3. **"Real but unwired" and "real but stubbed" are distinct from "missing."** Of the 9
   allowlisted interfaces, only `IDocumentDownloader` was actually missing; the auth trio
   was fully implemented (with JWT) but registered nowhere, and dashboards were real classes
   returning zeros. Collapsing these into "not implemented" would have been wrong in both
   directions.

4. **Mission/summary docs over-claim; treat them as targets or experiments.**
   `PRODUCTION_READY_SUMMARY.md` sells a "DocTR production-ready" OCR that does not exist in
   the C# wiring (Tesseract is wired, VLM is disabled). Preserve such docs as *intent/research*
   rather than deleting them or believing them.

5. **Keep both ground truths in the same row.** Recording target + actual side by side (not
   overwriting one with the other) is what makes the gap matrix actionable without erasing
   the destination.

## Engineering lessons carried from this session's prior work

6. **Package transitive-pinning cascade.** `CentralPackageTransitivePinningEnabled` means a
   single `PackageVersion` pin forces transitives too — every `Microsoft.Testing.*` must
   align to the same MTP minor, or you get CS1705. Bump the set together.

7. **xunit ↔ MTP ABI lockstep.** Test projects must reference `xunit.v3.mtp-v2` (not default
   `xunit.v3` = mtp-v1) + MTP 2.1.0 + `global.json` `{"test":{"runner":"Microsoft.Testing.Platform"}}`,
   or tests fail at *run* time with `MissingMethodException` (`IOutputDevice.DisplayAsync`).

8. **Native-binding and licensing upgrade traps.** Emgu.CV 4.13 changes the native
   `CvInvoke.CLAHE` signature (and Contrib/ubuntu lag); SixLabors.ImageSharp 4.x enforces a
   **paid commercial license at build**. These are decision-gated, not routine bumps.

9. **Anti-archaeology repo hygiene.** Don't blindly dedupe the duplicated Python tree —
   `CSharp/Python/` is build-wired via CSnakes; only `Src/Python/` is the doc generator.
   Verify build wiring before "cleaning up."
