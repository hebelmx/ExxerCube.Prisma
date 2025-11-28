# Stakeholder Demo & Funding Plan (UI/Pipeline)

Goal: Show working pages and a clear path to completion to secure continued funding. Focus on what’s live, what’s next, and the value tied to requirements.

## What to Demo (already working / near-ready)
- Navigation shell + any live pages (e.g., Document Processing, Manual Review, Audit Trail) — highlight role-based access and NotFound recovery if present.
- Ingestion/listing: show file intake UI or logs proving PDF/XML/Word are downloaded and enumerated.
- Extraction/validation: show one sample where fields are parsed and compared across docs (even if partial), with visible alerts on mismatches.
- SLA tracking: demonstrate date computations (recepción + días) and due-date display, if available.
- Summary/report: any existing PDF/XML export or layout generator output, even if minimal, to prove the pipeline can emit artifacts.

## What’s Missing (pages/workflows to plan)
- Download reconciliation view: expected vs downloaded files, with missing/extra flags.
- Structured case view: canonical expediente detail (fundamento, medio/envío, oficio/acuerdo refs, SLA dates, subdivision enum).
- Measures & assets: UI to display inferred measure type (bloqueo/desbloqueo/transferencia/documentación/información), accounts/products/montos, and requested docs.
- Identity fidelity: surfaces RFC variants/CURP/domicilio per persona; highlights confidence and gaps.
- Evidence chain: channel/signature/hash (SIARA/Físico, FELAVA) linked to each case/document.
- Summary dashboard: 5-part summary (bloqueo, desbloqueo, documentación, transferencia, información) with explicit “empty” markers when not applicable.
- Validation pane: required fields checklist with statuses (filled/derived/missing) to drive manual completion.

## What to Present (storyboard)
1) Problem & mandate: 1 slide tying to Disposiciones SIARA/R29 and Requirements.md (Etapas 1–4).
2) Current coverage: screenshots of working pages (nav, intake/logs, sample extraction, SLA display, export artifact).
3) Gap map: use `docs/qa/Pipeline_Gap_Analysis.md` highlights to show what remains (bullets above).
4) Plan & milestones: 2–3 sprints with deliverables (download reconciliation, canonical case view, measures/assets, evidence chain, summary dashboard).
5) Risk controls: validation flags, Unknown/Other enums, extension-friendly XML, and audit logging to handle schema/legal drift.
6) Ask: funding/time to complete the missing pages and validation/exports; optional staffing for OCR/parser improvements.

## Minimal Artifacts to Prepare
- Screenshots/GIFs: nav, ingest/logs, extraction result with mismatch alert, SLA dates, export sample.
- One end-to-end demo case: pick a PRP1 fixture, show intake → parsed fields → validation flags → export.
- Roadmap slide: dates/owners for missing pages; test plan snippet (mapping/completeness/export/SLA).
- Appendix: links to `docs/qa/Domain_Legal_CodeReview.md` and `docs/qa/Pipeline_Gap_Analysis.md` for depth.

## Success Criteria for Stakeholders
- They see working UI/pages proving feasibility.
- They see a short, credible plan to close gaps (features + tests).
- They see risk mitigations for legal/schema drift (Unknown/Other enums, validation flags, extension handling).
- They see auditability (evidence chain, SLA tracking, field-level provenance).
