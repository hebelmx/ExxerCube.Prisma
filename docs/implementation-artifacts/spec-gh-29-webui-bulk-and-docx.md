---
title: 'GH#29 — Web.UI batch processing + /adaptive-extractor cannot find their documents in the container'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: 'cc547c98'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem (two symptoms, same "container ≠ dev box" root):**
1. `/document-processing` "Batch processing" errors *"Failed to locate bulk documents directory"* —
   `FixtureFinder.FindBulkDocumentsPath` walks up from `/app` for a child
   `bulk_generated_documents_all_formats` dir that the image never baked.
2. `/adaptive-extractor` fails to load its DOCX fixtures — `AdaptiveDocxFixtureService.GetFixturePath`
   resolves fixtures via six `../` off `ContentRootPath`; in the container (`ContentRootPath=/app`)
   that clamps to `/Fixtures/PRP1` (absent), while GH#23 baked them at `/app/Fixtures/PRP1`.

**Approach:** (1) COPY the bulk sample set into `/app/bulk_generated_documents_all_formats`.
(2) Make `GetFixturePath` prefer the container-baked `<ContentRoot>/Fixtures/PRP1` path, falling
back to the dev-box relative path. The four DOCX fixtures already ship via GH#23's PRP1 COPY, so no
new DOCX asset copy is needed — only the resolver fix.

## Boundaries & Constraints

**Always:** Land bulk docs directly under `/app` (that's where `FindBulkDocumentsPath` looks).
Keep the dev-box fallback path in `GetFixturePath` so local `dotnet run` still works.

**Never:** Do NOT modify the shared `FixtureFinder` (used by ~10 test projects). Do NOT duplicate the
DOCX fixtures — they already come from `Prisma/Fixtures/PRP1` (GH#23).

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| Batch demo, container | `/app/bulk_generated_documents_all_formats` baked | dir located, case `AGAFADAFSON2-2024-101415` (xml+pdf) sampled |
| Adaptive-extractor, container | DOCX at `/app/Fixtures/PRP1` | `GetFixturePath` returns container path; fixture loads |
| Adaptive-extractor, dev box | ContentRoot=…/Web.UI | container path absent → dev-box `../×6` fallback used |

</frozen-after-approval>

## Code Map

- `.../Web.UI/Dockerfile` — COPY `Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/` → `/app/bulk_generated_documents_all_formats/`.
- `.../Web.UI/Services/AdaptiveDocxFixtureService.cs:122` — `GetFixturePath` prefers `<ContentRoot>/Fixtures/PRP1`, dev-box `../×6` fallback.
- `BulkProcessingService.GetRandomSampleAsync` / `FixtureFinder.FindBulkDocumentsPath` — context only; unchanged.

## Tasks & Acceptance

- [x] Dockerfile — bake bulk docs under `/app`.
- [x] `AdaptiveDocxFixtureService.GetFixturePath` — container-path-first resolution.

**Acceptance / Verified 2026-07-02:**
- Container: `/app/bulk_generated_documents_all_formats/AGAFADAFSON2-2024-101415` (xml+pdf) present →
  `GetRandomSampleAsync` enumerates it. `/app/Fixtures/PRP1/*.docx` (4 adaptive fixtures) present and
  now the first-choice resolver path. Build 0/0; app boots, `GET /` → 200.

**Follow-up (not a locate bug):** the bulk set ships only 1 case → thin batch demo. Enriching the
corpus is tracked with GH#31 (diverse corpus), not here.
