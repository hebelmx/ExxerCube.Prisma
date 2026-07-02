# Intended Solution — LLM/Hybrid OCR field extractor (runtime-switchable provider)

Anti-drift reference for the orchestrated build. Source: `docs/evaluation/ocr-extractor-analysis-2026-07.md`
+ BMAD party (2026-07-02). Ships **DARK** behind flags; deterministic path is UNTOUCHED.

## Grounded seams (verified in code)
- `IFieldExtractor<T>` — `01 Core/Domain/Interfaces/IFieldExtractor{T}.cs`, ns `ExxerCube.Prisma.Domain.Interfaces`.
  Signature: `Task<Result<ExtractedFields>> ExtractFieldsAsync(T source, FieldDefinition[] fieldDefinitions)`
  + `Task<Result<FieldValue>> ExtractFieldAsync(T source, string fieldName)`. **NO CancellationToken on the
  interface** — LLM extractors match this exactly; ct flows only through `ILlmProvider` (pass `CancellationToken.None`).
- `TxtSource` — `01 Core/Domain/Sources/`. `ImageSource` does NOT exist yet → S2 creates it there.
- `PdfToImageConverter.ConvertToImagesAsync(byte[])` → `Result<IReadOnlyList<byte[]>>` (byte[] PNGs).
- `IOllamaClient` (Domain/Interfaces) is text-only `Task<Result<string>> GenerateAsync(string prompt, ct)` — DO NOT extend it.
- `OllamaOptions`/`OllamaHttpClient` — `02 Infrastructure/Infrastructure.Classification/`.
- `AdaptiveTxtFieldExtractor` — `02 Infrastructure/Infrastructure.Extraction.Txt/` (deterministic; UNTOUCHED).
  `Infrastructure.Extraction` already references `Infrastructure.Extraction.Txt` (PdfOcrFieldExtractor delegates to it).
- `ExtractedFields` (Domain/ValueObjects) has mutable `Dictionary<string,string?> AdditionalFields`; `Expediente` entity has `SolicitudPartes`.

## Project placement (existing projects only — no new csproj)
- **Ports** → `01 Core/Domain/` : `Interfaces/ILlmProvider.cs`, `Interfaces/ILlmProviderFactory.cs`,
  `Interfaces/ISecretProvider.cs`, `Interfaces/IExtractionReconciler.cs`, `Llm/LlmRequest.cs`,
  `Llm/LlmCapabilities.cs`, `Llm/ReconciliationResult.cs`, `Sources/ImageSource.cs` (S2).
- **Provider infra** → `02 Infrastructure/Infrastructure.Classification/Llm/` : `OllamaProvider.cs`,
  `GeminiProvider.cs` (S2), `LlmProviderFactory.cs`, `ConfigSecretProvider.cs`, `LlmProvidersOptions.cs`.
- **Shared DTO/gate + text extractor** → `02 Infrastructure/Infrastructure.Extraction.Txt/Llm/` :
  `LlmExpedienteDto.cs`, `LlmParteDto.cs`, `LlmExtractionGate.cs`, `LlmExpedienteMapper.cs`, `LlmTxtFieldExtractor.cs`.
- **Vision extractor + reconciler impl** → `02 Infrastructure/Infrastructure.Extraction/Llm/` (S2).
- **Tests** → `Tests.Infrastructure.Classification` (providers/factory), `Tests.Infrastructure.Extraction.Txt`
  (gate/DTO/text extractor), `Tests.Infrastructure.Extraction` (vision/reconciler, S2).

## Contracts
```csharp
// Domain/Llm/LlmCapabilities.cs
[Flags] public enum LlmCapabilities { None=0, TextGenerate=1<<0, VisionGenerate=1<<1, StructuredOutput=1<<2 }

// Domain/Llm/LlmRequest.cs
public sealed record LlmRequest(string SystemPrompt, string UserPrompt,
    IReadOnlyList<byte[]>? Images = null, string? JsonSchema = null, string? ModelOverride = null);

// Domain/Interfaces/ILlmProvider.cs
public interface ILlmProvider {
    string Name { get; }
    LlmCapabilities Capabilities { get; }
    Task<Result<string>> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

// Domain/Interfaces/ILlmProviderFactory.cs (runtime switch; singleton impl)
public interface ILlmProviderFactory {
    ILlmProvider GetActive();
    Result<ILlmProvider> Get(string providerName);
    IReadOnlyList<string> RegisteredNames { get; }
    Result SetActive(string providerName);      // validates against registry
}

// Domain/Interfaces/ISecretProvider.cs (Prisma-own; do NOT reference Veriqan's)
public interface ISecretProvider {
    Task<Result<string>> GetSecretAsync(string key, CancellationToken cancellationToken = default);
}
```

## Merge / honesty policy (reconciler, S2)
Per-field: deterministic non-null wins (log if LLM disagrees); det null + one LLM → fill; two LLMs agree +
det null → fill (lower confidence); two LLMs conflict + det null → **null + review flag** (never coin-flip).
Format gate rejects malformed RFC/CURP/expediente/monto to abstain regardless of model confidence.
`ReconciliationResult(Best, IReadOnlyList<LabelledExtraction> Candidates)`; `LabelledExtraction(Source, Fields, TrackStatus)`;
`TrackStatus { Available, SkippedNoCapability, SkippedByFlag, Failed }`.

## Config (dark by default)
```jsonc
"LlmProviders": {
  "Active": "Ollama",
  "TextExtractorEnabled": false, "VisionExtractorEnabled": false,   // demo override flips true
  "Ollama": { "BaseUrl":"http://localhost:11434", "Model":"llama3.2", "VisionModel":"minicpm-v", "TimeoutSeconds":120 },
  "Gemini": { "ApiKeyRef":"Gemini:ApiKey", "Model":"gemini-2.0-flash", "TimeoutSeconds":60 }
}
```
`ConfigSecretProvider` resolves `ApiKeyRef` from `IConfiguration` (user-secrets/env) — never plaintext-logged.

## Locked decisions
A=`gemini-2.0-flash` default (Model knob). B=separate `Ollama.VisionModel`. C=always call LLM in demo
(reconciler decides per-field). D=Prisma-own `ISecretProvider` (config-backed) — do NOT cross-ref Veriqan.
E=`ImageSource(byte[])` seam for vision. Providers own their `HttpClient` via `IHttpClientFactory` (bypass `IOllamaClient`).

## Standards
.NET 10, Result<T> (never throw for business logic), `ConfigureAwait(false)` in libraries, warnings-as-errors,
xUnit v3 + Shouldly + NSubstitute, test naming `Method_Scenario_Expected`, `TestContext.Current.CancellationToken` in tests.
Gate is PURE (static, no I/O) — the deterministic unit-test anchor. Unit tests mock `ILlmProvider` with canned JSON.
```
