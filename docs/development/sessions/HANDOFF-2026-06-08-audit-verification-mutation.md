# Handoff — Live Dual-Track Verification, Ingestion Reality, Mutation Testing (2026-06-08)

**Prepared:** 2026-06-08 (work done 2026-06-07b → 06-08)
**For:** the next agent — **read this first**, then GAP-MATRIX §9.
**Branch:** `Kt2` (build green 0/0). Commits below are **local, not yet pushed.**

Supersedes `HANDOFF-2026-06-post-reconciliation.md` (its open items are carried forward + corrected here).

---

## 1. What this session did (verification, not just tracing)

Ran the real build + (almost) all suites on a Docker- + Playwright-capable machine and **launched the
Web.UI live** — upgrading the prior "static-wiring" truth to executed truth. Full evidence:
**`docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md` §9** (dual-track section).

- **Build 0/0; ~1,670+ tests green**, including the formerly-UNVERIFIED tiers: Docker SQL
  (Testcontainers `System.Storage` 39/39 + `Infrastructure.Database` 110/110), Playwright `Tests.UI`
  21/21, `BrowserAutomation.E2E` 18/18, `Tests.EndToEnd` 29/29, real Tesseract OCR 154/154.
- **Web.UI launches for real:** `GET /` → 200 (MudBlazor renders), `/health` → Healthy; boots even when
  the DB seed fails (caught).
- **Non-green = expected only:** `System.Ocr.Pipeline` flaky (live-OCR variance); `Extraction.GotOcr2` 16
  skipped + `Extraction.Python` 0 (VLM dormant by design). Owner ran a full `dotnet test` and saw ~15
  failures = Tesseract OCR timeouts under parallel load + a couple cross-concerns setup-sensitive tests —
  **not real bugs.**

## 2. Discoveries / corrections (don't re-derive these)

1. **Project count is ~70 (33 prod + 37 test), NOT "195/200".** The inflated figure (in CLAUDE.md +
   `STAKEHOLDER_PRESENTATION_READINESS.md`) came from a repo-wide csproj scan that swept
   `scripts/*_backups/**` (old `ExxerAI` clone), sample repos, and `Samples/GotOcr2Sample`. It predates the
   ExxerAI-tree housekeeping. "600+ tests" is also an undercount. Don't repeat either.
2. **Adaptive-DOCX tests were DARK — now fixed.** `Tests.Infrastructure.Extraction.Adaptive` (126 tests, 5
   strategies) was excluded from the `.sln` AND failed `CS0234` (ProjectReference depth 7 vs 3). Fixed +
   re-added → 126/126 green, solution still 0/0. "Adaptive DOCX tested" is now actually true.
3. **Ingestion / SIARA reality (owner correction) — supersedes "nothing real enters the pipeline":**
   document download **WORKS and was demoed** via the browser-automation/scraping path
   (`SiaraNavigationTarget` + `DocumentIngestionService`); `tools/Siara.Simulator` models SIARA's
   links-on-a-page shape; **no SIARA API exists → web-scraping is the deliberate approach.** Detail in
   **GAP-MATRIX §9.6.**

## 3. Architecture direction for ingestion (owner, load-bearing)

- **Split into 3 separately-hosted processes — Downloader / Extractor / Reconciliator — coordinated via the
  published `IndFusion.Ember` nuget** ("Three Actors": `IExxerHub<T>`/`IServiceHealth<T>`/`Dashboard<T>`;
  repo `E:\Dynamic\IndFusion\IndFusion.Ember`, ADR-009).
- **Security is the PRIMARY driver, not scaling.** Documents are highly confidential — lawyer first, then
  need-to-know to other bank staff; **no single person/process handles a doc end-to-end** (separation of
  duties). So the split is a security boundary: per-stage role/clearance authz, data minimization between
  stages (pass events/refs via Ember, not raw content), full audit (`AuditReportingService` is the seed).
- **Auth:** no SIARA API, basic web login; **never hold raw credentials** → session passthrough
  (sanctioned MITM-style handoff of a live session) OR one-time interactive login kept warm.
- **Volume:** ~500–2,000 docs/day at random hours (low; idempotency > speed; SHA-256 dedup already in
  `IngestionJournal`). Replaces staff manually F5-ing the portal.

## 4. Highest-leverage next build work (corrected from prior handoff)

1. **Headless ingestion chain** (was mislabeled "Orion download is stub = nothing ingests"): adapt the
   **working** browser-automation scraper into Orion's `IDocumentDownloader` port; implement
   `IngestionOrchestrator.StartAsync()` (poll/watcher, currently a placeholder — per-doc
   `IngestDocumentAsync` ROP is real + tested 8/8); replace `StubExxerHub` with real Ember in the worker.
   Ideally as the 3-process split above.
2. Carried forward from the prior handoff (still valid): trained polynomial filter models; semantic field
   extraction; PDF text extraction (iText/PdfSharp); analytical-filter ≥10% threshold (needs REAL data);
   DRY `ExtractedFields→Expediente` mapper; multi-source fusion (XML/DOCX null); auth abstraction wiring;
   worker `/dashboard` metrics + readiness `IsStarted`.
3. **Track B demo prep:** externalize the hardcoded `DESKTOP-FB2ES22\SQL2022` connection string; delete
   `Counter.razor` / `Weather.razor` Blazor template leftovers.

## 5. Mutation testing (NEW — now working)

- **Stryker.NET 4.14.2** pinned in `.config/dotnet-tools.json`. Guide + cross-repo guideline:
  **`docs/qa/test-plans/mutation-testing.md`.**
- **Two MANDATORY settings** (both caused an early uniform-0%): `"test-runner": "mtp"` in
  `stryker-config.json` (else the VSTest bridge silently never registers kills on MTP projects — *always
  broken*), and `StrykerCompat=true` (env) to flatten `OutputPath`/`IntermediateOutputPath` for this repo's
  custom `bin\<project>\<config>\<tfm>` layout. Run: `StrykerCompat=true dotnet stryker` from the test dir.
- Pilot: `AdaptiveTxtFieldExtractor` → **24.90%** (Killed 64 / Survived 167). Owner is restoring mutation
  testing org-wide; reference working repo = `IndFusion.Ember`. `thresholds.break` stays 0 (baseline only).

## 6. Guardrails carried forward (don't undo)
- OCR = Tesseract by deliberate decision; Python/CSnakes VLM is intentional dormant optionality (ADR-001).
- Docker DB tests: per-class isolated DBs, never a shared mutable DB across parallel classes.
- Keep both ground truths (target AND actual); don't assert "production-ready" without E2E verification.
- `stryker-config.json` is strictly validated — no unknown keys (not even `_comment`).

## 7. Key pointers
- **GAP-MATRIX (read first):** `docs/planning/gap-analysis/GAP-MATRIX-2026-06-dual-ground-truth.md` (§9 = this session).
- Roadmap: `docs/planning/path-to-production-2026-06.md`.
- Mutation testing: `docs/qa/test-plans/mutation-testing.md`.
- Conventions + status: `CLAUDE.md` (build-status "Live verification pass 2026-06-07b").

## 8. Commits this session (on `Kt2`, local)
`4ffe3d0` fix(tests): restore adaptive-DOCX project · `bcc5d23` docs(audit): dual-track verification +
ingestion reality · `9f54fc9` docs(ingestion): separation-of-duties · `405a5eb` chore(qa): Stryker scaffold
· `3cf3c25` fix(qa): Stryker test-runner=mtp (24.9% baseline). **Not pushed yet.**
