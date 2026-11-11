 - You want a Python-based linter pipeline that focuses first on the database layer (e.g., code/src/Infrastructure/
    ExxerAI.Domain and related test projects) but can grow to other components.
  - The pipeline should be multi-pass: (1) catalog every type and its declaring project, (2) build a usage/dependency
    graph, (3) flag weak or incomplete XML docs (placeholders like “Sets the foo”, missing <param> info, duplicated
    text, etc.).
  - Each pass writes structured JSON so later tooling (or humans) can auto-generate high-quality documentation or
    remediation plans.
  - CLI should default to the repo root (current working directory if not supplied) but also accept an explicit
    --root/--cwd parameter.

  Proposed Pipeline

  - python scripts/doc_quality/type_catalog.py
      - Inputs: --root (default PWD), optional glob filters (initially target Domain).
      - Walk code/src (and mirrored test dirs), read .csproj to capture project identity/layer, parse each .cs file
        (tree-sitter C# or Roslyn via dotnet-script) to list namespaces, types, partial declarations, accessibility,
        attributes.
      - Output artifacts/doclint/types_index.json with entries { "type":
        "ExxerAI.Domain.Repositories.DocumentStore", "file": "...cs", "project": "...Domain.csproj", "kind":
        "class", "accessibility": "public", "xml_doc": "<summary>...</summary>", ... }.
  - python scripts/doc_quality/type_usage.py
      - Reuses catalog JSON; scans source again (or piggybacks on Roslyn symbol graph) to map who references each type/
        method/property.
      - Records { "consumer": "...SimpleBm25SearchService", "member": "ProcessAsync", "uses": [{ "type":
        "...DocumentStore", "usage": "constructor_param" }, ...] }.
      - This context helps the doc-linter explain how a type is used (“Referenced by 14 handlers across Domain”).
  - python scripts/doc_quality/doc_linter.py
      - Consumes both JSONs to know importance, layering, and relationships.
      - For each public/protected member:
          - Parse XML docs into structured fields.
          - Apply heuristics (see below) to determine quality score and flag reasons.
          - Output artifacts/doclint/doc_quality_report.json with entries { "file": "...cs", "line": 123, "member":
            "IngestDocumentAsync", "project": "...Domain.csproj", "issue": "WeakSummary", "currentText": "Sets
            the foo.", "recommendation": "...", "severity": "warning", "typeRefs": ["GoogleDriveDocument"], "callers":
            ["WorkflowExecutor"] }.
      - Include batchId or timestamp so downstream automation can generate audit forms similar to the existing
        templates.

  Doc-Quality Heuristics

  - Summary Meaningfulness: minimum word count, must reference the domain concept (e.g., “Ingests Google Drive documents
    into Vault”) rather than generic verbs; penalize “Gets/sets the value.” or repeats of member name.
  - Parameter Coverage: every parameter (including CancellationToken) needs a <param> entry mentioning why/when it’s
    used, not just “The token.”; detect duplicated text across params.
  - Return Semantics: <returns> must describe Result<T> meaning (success path, error propagation) and mention
    nullability; flag empty or boilerplate.
  - Exceptions/Failure Notes: if method converts exceptions to Result.WithFailure, require <remarks> describing
    diagnostic codes or sentinel behavior.
  - Nested/Generic Types: ensure <typeparam> exists and explains constraints; nested records/classes need their own docs
    referencing the parent narrative.
  - See Also / Related Workflows: encourage <seealso> to point to interfaces, DTOs, or specs from the usage graph.
  - Language Checks: simple NLP heuristics (stop words, repeated tokens, banned phrases list) plus optional spell-check
    for domain terms.

  JSON Schemas (suggested)

  - types_index.json: { "types": [ { "id": "...", "project": "...", "layer": "Domain", "file": "...", "line": 42,
    "kind": "record", "accessibility": "public", "xmlDocHash": "...", "hasSummary": true, "hasRemarks": false } ] }
  - type_usage.json: { "usages": [ { "targetType": "...DocumentStore", "consumers": [ { "type":
    "...SimpleBm25SearchService", "member": "ProcessAsync", "relation": "constructor_param" } ] } ] }
  - doc_quality_report.json: { "generatedAt": "...", "issues": [ { "file": "...cs", "line": 118, "member":
    "SimpleBm25SearchService.ProcessAsync", "rule": "SummaryMeaningless", "severity": "warning", "message":
    "Summary repeats method name without describing behavior.", "context": { "project": "...", "layer": "Nexus",
    "typeImportance": 0.82, "callers": [...] } } ] }

  CLI & Defaults

  - Every script accepts --root (defaults to os.getcwd()), --solution-root optional if run from subfolder, and --output
    directory (default artifacts/doclint).
  - Domain-first strategy: default glob code/src/**/*Domain*/**/*.cs, overridable via --include/--exclude.
  - Provide --incremental mode: compare previous JSON hash and only re-scan changed files.

  Extra Ideas

  1. Quality Score Dashboard: aggregate doc issues by project/component to focus sprints where coverage is weakest.
  2. IDE Integration: expose quick-fix hints by emitting SARIF alongside JSON so CI and editors highlight weak docs
     inline.
  3. Autofix Suggestions: use the type/usage context plus heuristics to generate draft <summary>/<remarks> templates
     referencing actual domain terms (maybe leverage small LLM offline).
  4. Cross-Layer Validation: ensure Domain entities mention upstream/downstream contracts (e.g., “Flows into Vault
     ingestion”) using the usage graph.
  5. Unit Tests for Linter: store sample .cs fixtures in tests/tools/DocQuality so changes to heuristics stay
     deterministic.
