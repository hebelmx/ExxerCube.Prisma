# HANDOFF — 2026-06-14 — Max-Fidelity Gate (#5) — ✅ DONE (gate GREEN, committed `646422c`)

**Branch:** `Kt2` · **Predecessor handoff:** `HANDOFF-2026-06-13-gate-deflake-and-best-effort-resilience.md`
**Driver:** bmad-orchestrator (delegate chunks → **verify every chunk from ground truth**, never trust subagent summaries).
**Why this exists:** machine crashed + Docker corrupted mid-session; a restart is planned, so this captures the exact state so a fresh session loses nothing.

---

## TL;DR — RESOLVED 2026-06-14

The **one remaining MVP item — the gate (#5), a SINGLE max-fidelity full live end-to-end run — now PASSES** (`passed (4m 29s)`, Testcontainers SQL, both real SignalR/JWT edges, 0 audit-emission failures). Code + tests committed as `646422c`. The live run surfaced **three** gaps in succession (each only visible once the previous was cleared — exactly why a single full-fidelity run was the right gate):

- **Gap 1 (PDF rasterization) — ✅ FIXED.** `FileSystemLoader` was a `byte[0]` stub; now rasterizes page 1 @300 DPI via the existing `PdfToImageConverter` (+`IPdfToImageConverter` registered in the Athena Worker). +2 unit tests.
- **Gap 2 (companion XML/DOCX invisible to fusion) — ✅ FIXED, and it was NOT a cascade from gap 1.** Root cause: `FileFormat` is a **SmartEnum** (`EnumModel`) that default System.Text.Json serializes as an object and cannot reconstruct, so every `CaseFileReference.Format` collapsed to the zero-value singleton (`Pdf`) across the Orion→Athena SignalR wire → companions invisible to Stage-3 fusion → empty expediente → SIRO export refused. Fix: register `EnumModelJsonConverterFactory` on the JSON protocol at **both ends of both** SignalR edges; **moved the converter Infrastructure→Domain** (`Domain.Serialization`) so the lean Orion Downloader can use it without referencing the heavy Infrastructure assembly (CSnakes/Python). Reproduced + pinned by `DocumentDownloadedEventSerializationTests`.
- **Gap 3 (audit never persisted) — ✅ FIXED (test-host) + ✅ FIXED (real DI bug).** (a) The workers read `ConnectionStrings:DefaultConnection` eagerly at the top of `Program.cs` before WAF applies in-memory config → the gate now injects it via an env var scoped to the host-build window. (b) Once the DB graph was actually wired, `AddDatabaseServices` force-registered `EventPersistenceWorker` (needs `IEventPublisher`, absent in the lean Orion Downloader) → a **production Orion with a configured DB would have failed to boot**. Added a `registerEventPersistence` opt-out and disabled it for Orion.

**Verification (all green):** gate PASSED · solution build 0/0 · Architecture 22/22 · fast AllRealWire 3-host E2E 1/1 · serialization regression 3/3 · handoff SmartEnum 16/16 · Processing.Tests 81/81.

**Next:** the deferred best-effort missing-file gate scenario (owner ruling 3 / task #3 below) + `[DEV MISSING]` review-case persistence (GH #6) remain open. The gate currently exercises the full (complete-case) happy path.

---

## Working-tree state (UNCOMMITTED — survives restart, on disk)

```
 M  02 Infrastructure/Infrastructure/FileSystem/FileSystemLoader.cs          ← gap 1 fix
 M  04 Services/Athena/Prisma.Athena.Worker/Program.cs                       ← gap 1 DI registration
 M  08 Tests/02 Infrastructure/Tests.Infrastructure.FileSystem/FileSystemLoaderTests.cs  ← +2 unit tests
 M  08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj    ← +4 ProjectRefs
 ?? 08 Tests/06 E2E/Tests.AllRealWireE2E/MaxFidelityGateE2ETests.cs          ← the gate test
 ?? 08 Tests/06 E2E/Tests.AllRealWireE2E/MaxFidelityGateHosts.cs            ← 3 WAF host subclasses
```

Nothing is committed yet — commit only once the gate is green (and code+tests SEPARATE from any docs commit, per hard constraints).

---

## Owner rulings (in force)

1. **Gate (#5) shape = SINGLE FULL LIVE E2E, max fidelity.** One run, nothing in the pipeline stubbed: live-sim case pull (real `SiaraDocumentDownloader` + `IngestCaseAsync`) → real OCR (Tesseract) → real 3-source fusion (`FusionExpedienteService`) → real classify (`FileClassifierService`) → real SIRO XML export (`SiroXmlExporter`) → audit persisted to **Testcontainers SQL** (`ConnectionStrings:DefaultConnection`), across BOTH real SignalR wires + JWT clearance + shared-storage handoff. Owner chose this knowing the flake/effort cost.
2. **Stubs OK to demo ONLY with explicit `[DEV MISSING]` labels.** Never report a stubbed path as done; say so loudly where full fidelity isn't reached.
3. **Exercise a missing-file (best-effort) scenario** too (the ~5–15% partial-case path; `IsComplete=false`).
4. **2026-06-14: FIX BOTH discovered production gaps first** (below), then land the gate truly real — not a scoped/labeled fallback.
5. After the gate: wire `IsComplete` into the review dashboard (deferred, GH #6 — the honest gap of the best-effort feature).

---

## Gap 1 — PDF rasterization — ✅ FIXED (uncommitted, verified)

**Problem:** `FileSystemLoader.LoadPdfAsImage(filePath)` was a hardcoded stub returning `new byte[0]`. Every `.pdf` primary loaded as empty bytes → quality analyzer + Tesseract OCR fail → PDF source contributes nothing to fusion.

**Fix (clean, reuses existing tested code):**
- `FileSystemLoader` now takes an **optional** `IPdfToImageConverter?` ctor dependency (MS.DI honors the default-null param, so existing `new FileSystemLoader(logger)` tests and the base `AddScoped<IFileLoader, FileSystemLoader>()` registration still work).
- `.pdf` primary → `LoadPdfAsImageAsync` reads the bytes and rasterizes **page 1 @ 300 DPI to PNG** via the already-existing `PdfToImageConverter` (PDFtoImage/SkiaSharp, in `Infrastructure.Extraction.Ocr`). When no converter is wired (or 0 pages), it returns the **raw PDF bytes** (not `byte[0]`) so a downstream OCR fallback can still try.
- Registered `IPdfToImageConverter` → `PdfToImageConverter` in **Athena Worker `Program.cs`** (it was only registered in `Infrastructure.Extraction.DependencyInjection`, which the worker doesn't call).

**Design note:** the worker pipeline processes a single `ImageData`, so we OCR page 1 (the oficio/expediente header where canonical fields live). Multi-page OCR remains the job of the dedicated `PdfOcrFieldExtractor` path. If page-1 OCR proves too thin for real SIARA docs, the follow-up is to thread multi-page `ImageData` through Stage 1→2 — but that's a larger refactor, out of gate scope.

**Verification done:** Athena Worker builds 0/0; gate test project builds 0/0; 2 new unit tests green:
`LoadImageAsync_PdfWithConverter_ReturnsRasterizedFirstPage` + `LoadImageAsync_PdfWithoutConverter_ReturnsRawPdfBytesNotEmpty`.

---

## Gap 2 — companion XML/DOCX fusion — NOT YET CONFIRMED REAL (diagnose from logs)

The prior run reported fusion failing "At least one source Expediente must be provided" and a subagent attributed it to the **sim's XML/DOCX not matching the CNBV/PRP1 extractor shapes**. **I refuted that theory by reading the source:**

- `XmlFieldExtractor.ExtractFieldsAsync` (`Infrastructure.Extraction/Teseract/XmlFieldExtractor.cs`) **does NOT care about the root element name** and **never fails on missing fields** — it returns `Result.Success(extractedFields)` with whatever it found. It only fails on null source / empty-or-invalid XML / no root / exception.
- The sim XML *does* contain `<Cnbv_NumeroExpediente>…</Cnbv_NumeroExpediente>` under namespace `http://www.cnbv.gob.mx`, and the extractor reads exactly that → it **will** populate `Expediente`.
- `ExtractionOrchestrator.MapExtractedFieldsToExpediente` **always returns a non-null Expediente**.

**Therefore** the sim XML *should* yield a fusion source. The real "zero sources" was most plausibly a **cascade from the PDF stub** (PDF empty → no `pdfExpediente`) combined with possibly the companion file not being on disk at fusion time. So **gap 1 alone may make the gate pass** (PDF source now real → fusion has ≥1 source; and the XML companion should add a second).

**Plan (do NOT regenerate `bulk_generated_documents_all_formats/` blindly):**
1. Re-run the gate **after gap 1**, capturing full logs.
2. Read the Athena Extractor logs for: Stage 1 quality, Stage 2 OCR text length, and the Stage 3 `Built {PDF,XML,DOCX} Expediente …` / `companion extraction produced no fields` lines.
3. If XML/DOCX still produce nothing, the real cause is almost certainly **companion `CaseFile.RelativePath` not resolving to an on-disk absolute path** — the forwarder (`IngestionEventForwarder.ResolveCaseFilePaths`) resolves them, but verify the files physically exist where it points at fusion time. Fix the actual cause, not the file format.
4. Only if the content genuinely can't extract should the sim corpus be touched.

---

## The gate test (already authored — orientation)

- **`MaxFidelityGateE2ETests.cs`** — `[Trait("Category","Integration")] [Trait("Category","MaxFidelityGate")]`, `[Fact(Timeout = 900_000)]`. `IAsyncLifetime`: starts the published sim on `:5001` if down, spins up `SqlServerContainerFixture` + `CreateIsolatedDatabaseAsync` + `EnsureCreatedAsync` for schema. The test: Playwright login → capture credential-free storage-state → boot 3 hosts → subscribe Export/Completion → wait hub clients connected → real `DiscoverFullCompanionCaseAsync` (polls `ISiaraDocumentSource` for a PDF+DOCX+XML package) → real `IngestCaseAsync` → await `ExportCompletedEvent` (10 min) → assert identity preserved, SIRO XML format+size, `.fusion.json` on disk, real case bytes on disk, audit rows in SQL, and ≥2 distinct process identities wrote audit. Already `[DEV MISSING]`-documents that review-case persistence (GH #6) is not wired.
- **`MaxFidelityGateHosts.cs`** — `GateOrionApp` / `GateAthenaApp` / `GateReconciliatorApp` (WAF subclasses of the three worker `Program`s). All wire `ConnectionStrings:DefaultConnection` to the Testcontainers DB and run the **real** pipeline; SignalR is routed through the upstream host's in-memory TestServer handler (production hub + auth code runs, no TCP port). Orion removes the `OrionWorkerService` watch loop (test drives ingestion) and uses an isolated journal so repeated sim cases aren't deduped across runs. `GateReconciliatorApp` swaps in an observable `EventPublisher` so the test can await terminal events.
- **csproj** — added 4 ProjectRefs: `Infrastructure.Database`, `Infrastructure.Classification`, `Infrastructure.BrowserAutomation`, `Testing.Infrastructure` (for `SqlServerContainerFixture`).

---

## Exact next steps (after restart)

1. **Start Docker Desktop**, wait for the Linux engine: `docker info` must succeed (WSL2 engine can take a couple minutes on a fresh boot).
2. **Run the gate, capture to a file (never pipe through `tail` — it masks the exit code):**
   ```bash
   dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj" \
     --filter-query "/*/*/MaxFidelityGateE2ETests/*" > /tmp/gate.log 2>&1; echo "EXIT=$?"
   ```
   (The sim self-starts; needs the published `Deployments/Siara.Simulator/app/Siara.Simulator.exe`, Chromium via `playwright install chromium`, native Tesseract + tessdata on PATH.)
3. **Read `/tmp/gate.log`** for the Stage 1/2/3 + companion-extraction lines (gap 2 diagnosis) and whether `ExportCompletedEvent` arrived.
4. **If green:** add the best-effort missing-file scenario (task #3) + `[DEV MISSING]` labels where applicable → **commit** (code+tests in one commit, docs separately) → **push Kt2** → adversarial review pass.
5. **If XML/DOCX still null:** diagnose companion `RelativePath` resolution / on-disk presence at fusion time (see Gap 2 plan step 3) and fix the real cause.

---

## Hard constraints (unchanged)

- ITDD per ADR-005; `Result<T>` + `CancellationToken` on every async method; `ConfigureAwait(false)` in library code.
- xUnit v3 + Shouldly + NSubstitute (NO Moq/FluentAssertions); `TestContext.Current.CancellationToken`.
- MTP filter-query needs **4 segments** `/Assembly/Namespace/Class/Method` (3 segments → exit 8, zero tests). `|` OR is not valid — use a method wildcard (`/*/*/Class/Method*`).
- Never pipe `dotnet test` through `tail` (masks non-zero exit) — redirect to a file.
- Broadcast via `IHubContext` (a DI-resolved hub has a null `Clients`).
- Commit code+tests SEPARATELY from docs; push to `Kt2` (never `main` without checkpoint).
- **Verify every chunk from ground truth** — diff the working tree, run the affected suites, run a worker ValidateOnBuild/host-DI test yourself. Subagent "all green" summaries have lied before (a prior session a subagent deleted an unrelated legal PDF).
- Owner-gated production changes and scope/requirement forks → checkpoint with the owner before sinking hours.

---

## Open / deferred

- **GH #6 (honest gap of best-effort feature):** `DocumentDownloadedEvent.IsComplete` is SET but not consumed — propagate it to the review dashboard as an incomplete-case flag; add downstream idempotency on partial→complete re-emit.
- Per-case retry-budget counter (journal returns bool only — issue #3).
- SIRO XSD validation dormant pending the Banamex `.xsd` (issue #2).
- Possible follow-up: multi-page PDF OCR through the worker pipeline (gate uses page 1 only).
