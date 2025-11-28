# Pipeline Gap Analysis vs Requirements

Scope: Gap review of the end-to-end pipeline against `docs/qa/Requirements.md` (ingest → classify → extract/compare → layout/export → summarize) using current domain types/interfaces in `Prisma/Code/Src/CSharp/Domain`. No code changes; this is a risk map and action list.

## Ingestion & Listing
- Requirement: download 3 oficio docs (PDF/XML/Word), list files, compare against expected list (Checklist Etapa 1/2/3).  
- Gap: No domain model for “expected vs downloaded” inventory or mismatch alerts. Interfaces (`IBrowserAutomationAgent`, `IDownloadTracker`, `IDownloadStorage`) do not surface a comparison result or validation state.  
- Action: Add a `DownloadSet` result (expected, found, missing, extra) and validation flags; ensure `IAuditLogger` records the comparison.

## Extraction & Cross-Document Validation
- Requirement: extract fields from two docs and compare; alert on mismatches; handle missing XML (PDF/Word fallback).  
- Gap: `IFieldExtractor`/`IFieldMatcher` have no canonical schema to compare against; current entities (`Expediente`, `SolicitudEspecifica`) lack validation state and structured accounts/products.  
- Action: Implement canonical model (see Domain_Legal_CodeReview.md) and enforce comparison with per-field origin (XML/OCR/Manual) plus mismatch alerts.

## Classification & Measure Intent
- Requirement: classify tipo/subtipo (bloqueo/desbloqueo/transferencia/documentación/información) and subdivision (A/AS…E/IN).  
- Gap: No `MeasureType`/`LegalSubdivision` enums in code; intent only inferred from text flags (`TieneAseguramiento`).  
- Action: Add enums with `Unknown/Other`; map from XML and instructions; require classification outcome before export.

## SLA & Deadlines
- Requirement: compute conclusion date (`recepción + días hábiles`) and track SLA.  
- Gap: `Expediente` lacks `FechaRegistro/FechaEstimadaConclusion`; `ISLAEnforcer` has no typed binding to `Expediente`.  
- Action: Add fields, derive dates on ingest, and store validation (computed vs missing).

## Layout & Export
- Requirement: generate SIRO layout Excel and XML/PDF response.  
- Gap: `ILayoutGenerator`/`IResponseExporter` not aligned to canonical fields (fundamento, medio/envío, measures, accounts, RFC variants).  
- Action: Update exporters to require canonical model; add validation to block exports when required fields are missing.

## Summary Generation
- Requirement: 5-part PDF summary (bloqueo, desbloqueo, documentación, transferencia, información).  
- Gap: No domain representation for summary buckets; no interface to ensure each section is populated or explicitly empty.  
- Action: Add `SummarySection` model and require population per measure type.

## Identity & Accounts
- Requirement: multiple RFC variants, CURP, domicile; accounts/products/montos structured.  
- Gap: Entities (`SolicitudParte`, `PersonaSolicitud`) support only single RFC; accounts are free-text.  
- Action: Introduce `RfcVariant`, `Cuenta` value objects; add validation for “identify account/product/monto” questions.

## Evidence & Traceability
- Requirement: channel (SIARA/físico), signature (FELAVA), evidence hash.  
- Gap: `FileMetadata` not linked to expedientes/oficios; no channel/signature fields; no chain-of-custody log.  
- Action: Link evidence to cases; add channel/signature/hash and audit trail.

## Testing Coverage Needed
- Fixture-based mapping/comparison tests for each `Prisma/Fixtures/PRP1/*.xml` (and PDF/Word when XML missing).  
- Completeness tests for required legal fields and checklist items (downloads present, comparisons done, measures classified, SLA computed).  
- Export snapshot tests for layout/XML/PDF summary; fail on missing required sections.  
- SLA tests validating hábiles calculation and due dates.
