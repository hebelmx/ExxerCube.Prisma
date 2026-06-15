# Item E (#11, Step 7) — Per-category sub-answer extraction — Design

**Owner decision:** HYBRID — regex/rules for structured sub-fields + activate the dormant Ollama LLM for the
interpreted free-text. Split into **E1** (structured/regex — deterministic, do first) and **E2** (Ollama
activation — risky new dependency, optional/fail-open, do second).

## Ground-truth findings
- `Infrastructure.Classification/SemanticAnalyzerService.cs` detects all 5 categories but leaves sub-answers TODO
  at `:143` (Bloqueo), `:168` (Desbloqueo), `:193` (Documentación), `:218` (Transferencia), `:243` (Información).
- The value objects already carry the sub-answer fields (Domain.ValueObjects): `BloqueoRequirement`
  {EsParcial, Monto, Moneda, CuentasEspecificas, ProductosEspecificos}; `DesbloqueoRequirement`
  {ExpedienteBloqueoOriginal}; `DocumentacionRequirement` {TiposDocumento: List<DocumentoRequerido{Tipo,
  PeriodoInicio, PeriodoFin}>}; `TransferenciaRequirement` {CuentaDestino, Monto}; `InformacionGeneralRequirement`
  {InformacionSolicitada}.
- Reuse target: `LegalDirectiveClassifierService.ExtractActionDetails` (`:417-502`) — account regex `cuenta\s+(\d{4,})`,
  amount patterns (`\$`, monto/cantidad/importe/suma, pesos/dólares, comma-grouped), product TARJETA/CUENTA.
- LLM seam: NO production client today. `09 Testing/.../OllamaContainerFixture.cs` (model `llama3.2:3b`, port 11434,
  `POST /api/generate` `{model,prompt,stream:false,options}`) + `OllamaInfrastructureSmokeTests`. `ISemanticAnalyzer`
  (Domain.Interfaces) is implemented by the fuzzy `SemanticAnalyzerService`; registered at the Classification DI `:35`.
- Output sink: `PdfRequirementSummarizerService` → `RequirementSummary` → `UnifiedMetadataRecord.RequirementSummary`.

## E1 — structured sub-answers (regex/rules; deterministic) — do first
Populate the regex-extractable sub-fields in each of the 5 detection methods, reusing/aligning with
`ExtractActionDetails`' patterns (extract small private helpers or a shared `RequirementDetailExtractor` so both
services share one source of truth — do NOT duplicate regex):
- **Bloqueo** (`:143`): CuentasEspecificas (account numbers), Monto + Moneda (amount/currency), ProductosEspecificos
  (TARJETA/CUENTA/…), EsParcial (partial vs total — keyword).
- **Desbloqueo** (`:168`): ExpedienteBloqueoOriginal — the original-block expediente/oficio id (reuse the
  Expediente/oficio id patterns; look for a referenced id distinct from the current one).
- **Documentación** (`:193`): TiposDocumento — detect doc sub-types from the curated vocabulary (estado de cuenta /
  identificación-INE / comprobante de domicilio / contrato / muestra de firma / cheque / expediente apertura) +
  PeriodoInicio/PeriodoFin when a date range is present.
- **Transferencia** (`:218`): CuentaDestino (CLABE 18-digit / account), Monto.
- **Información** (`:243`): a best-effort regex/sentence pull for `InformacionSolicitada` (the LLM in E2 will improve it).
Keep every extractor pure + null-safe; unset fields stay null/empty (best-effort, never throw).
**Tests (ITDD):** unit tests per category over representative text (and the real PDF/OCR text where available) —
accounts/amounts/products/doc-types/dest-account/expediente-ref extracted; absent → empty; mutation-killable
helpers. Existing `SemanticAnalyzerServiceMutationTests` + dictionary tests stay green.

## E2 — activate Ollama for interpreted free-text — do second (optional, fail-open)
1. **`IOllamaClient`** in `01 Core/Domain/Interfaces/` (namespace `ExxerCube.Prisma.Domain.Interfaces`):
   `Task<Result<string>> GenerateAsync(string prompt, CancellationToken ct)` (model/endpoint from options).
   (Interface in Domain — arch guardrail; do NOT place it in Infrastructure.)
2. **`OllamaHttpClient : IOllamaClient`** (Infrastructure) — `POST {endpoint}/api/generate` with
   `{model, prompt, stream:false, options:{temperature:0}}`, parse the `response` field; `Result.WithFailure` on any
   HTTP/parse error (fail-open). `OllamaOptions` {Endpoint, Model="llama3.2:3b", Enabled=false, TimeoutSeconds}.
3. **Enriched analyzer** — make `SemanticAnalyzerService` (or a decorator) take an OPTIONAL `IOllamaClient? = null`.
   When present AND a free-text/interpreted sub-answer is still empty after E1, ask the LLM a tight, grounded
   prompt (the oficio text + the specific question, e.g. "¿Qué información se solicita?") and fill
   `InformacionSolicitada` (and optionally refine other interpreted fields). When null/disabled/failed → keep the
   E1 structured result (fail-open). The LLM NEVER overrides a confidently-regex-extracted structured value.
4. **DI/config:** register `OllamaHttpClient` + bind `OllamaOptions`; inject into the analyzer. Default
   `Enabled=false` so environments without Ollama run structured-only; the demo/production config enables it.
5. **Tests:** unit tests with a SUBSTITUTE `IOllamaClient` (LLM fills Información; LLM failure → fail-open keeps E1
   result; null → structured-only). ONE integration test gated on `OllamaContainerFixture` (real llama3.2:3b)
   asserting a non-empty grounded answer — Docker-gated, follow the existing Ollama smoke-test collection pattern;
   do not make it a hard requirement of the normal suite.

## Definition of done
- E1: build 0/0; structured sub-answers populated + unit-tested (real text where available); existing
  classification tests green; arch 22/22.
- E2: build 0/0; `IOllamaClient` (Domain) + `OllamaHttpClient` (Infra) + options + DI; analyzer enriches free-text
  when Ollama enabled, fail-open otherwise; substitute unit tests + one Docker-gated Ollama integration test; arch 22/22.
- `git diff` confined to SemanticAnalyzerService (+ shared detail extractor), the value-object population, the new
  Ollama port/adapter/options/DI, and tests.
