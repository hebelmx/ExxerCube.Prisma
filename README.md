# ExxerCube Prisma

**Automated processing of CNBV / SIARA regulatory requests for Mexican financial institutions.**
*Procesamiento automatizado de requerimientos de autoridad (SIARA) para instituciones financieras.*

Every bank, broker and SOFOM in Mexico receives a steady stream of *oficios* from authorities
(SAT, UIF, FGR, IMSS, courts) through the CNBV's SIARA portal: freezes (*aseguramientos*),
unfreezes, information requests and fund transfers, each with a legal deadline measured in
business days. Each one arrives as up to three companion files (PDF, DOCX, XML) that someone
has to read, reconcile, classify and answer.

Prisma does that work end to end: it downloads the case, reads every companion (native text or
OCR), fuses the sources field by field, classifies the legal directive, tracks the SLA, routes
doubtful cases to a human reviewer, and exports the response in the formats the regulator expects.

## What it does

```
SIARA ─► Orion (download) ─► Athena (read + fuse) ─► Reconciliator (classify + export)
                               │
                               ├─ 1. Quality analysis   image metrics, adaptive filters
                               ├─ 2. OCR / text         Tesseract, native PDF text (PdfPig)
                               ├─ 3. Fusion             XML + DOCX + PDF, per-field confidence
                               ├─ 4. Classification     legal directive, authority, SLA
                               └─ 5. Export             SIRO XML · Excel · PDF
```

- **Multi-source fusion.** The XML, DOCX and PDF of a case are extracted independently and
  reconciled field by field; disagreements lower confidence and send the case to review instead
  of silently picking a value.
- **Human in the loop.** A Blazor review dashboard shows the source documents next to the
  extracted fields, with an immutable audit trail of every decision.
- **SLA tracking.** Deadlines are computed in Mexican business days (holiday calendar included)
  and surfaced before they are missed.
- **Security by design.** Ingestion is split into three processes (Downloader / Extractor /
  Reconciliator), each with its own identity and clearance; every handoff carries a signed token
  bound to one document and only a storage reference, never the document itself. SIARA sign-in
  supports three configurable modes, none of which persists credentials.

## Status

Functional MVP. The complete pipeline runs end to end against the bundled SIARA simulator
(`MaxFidelityGateFullPipelineE2ETests`). Remaining work is production hardening — configuration
externalization, secrets, and validation against the live portal under an adopting
institution's compliance sign-off. See [`CLAUDE.md`](CLAUDE.md#release-status) for details.

## Tech

.NET 10 · C# (hexagonal architecture, `Result<T>` railway-oriented error handling, Rx.NET domain
events) · Blazor Server + MudBlazor · EF Core / SQL Server · Tesseract OCR · Emgu CV · PdfPig ·
Playwright · SignalR via [`IndFusion.Ember`](https://github.com/hebelmx/IndFusion.Ember) ·
optional Python/CSnakes VLM extractors · xUnit v3 + Shouldly + NSubstitute + Testcontainers ·
OpenTelemetry + Serilog.

Architecture rules are enforced by tests (`08 Tests/09 Architecture`), and the suite includes
contract tests, mutation-killing tests and full-pipeline E2E tests.

## Quick start

Requirements: .NET 10 SDK; Docker (for the full stack and the SQL Server integration tests);
on Linux, the Tesseract/Leptonica and Emgu CV system libraries listed in
[`CLAUDE.md`](CLAUDE.md#release-status).

```bash
# Build and run the fast test suites
dotnet build "Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln"
dotnet test --project "Prisma/Code/Src/CSharp/08 Tests/01 Core/Tests.Domain/ExxerCube.Prisma.Tests.Domain.csproj"

# Bring up the full stack (Web UI, workers, SIARA simulator, SQL Server, Seq)
cp .env.example .env
bash scripts/generators/regen_tier1_corpus.sh   # synthetic SIARA cases (not committed)
docker compose -p prisma -f docker-compose.dev.yml -f docker-compose.staging.override.yml up --build -d
# Web UI: http://localhost:8085 · SIARA simulator: http://localhost:8084
```

More: [`QUICKSTART.md`](QUICKSTART.md) · [`docs/`](docs/README.md) ·
[architecture guidelines](docs/ARCHITECTURE_AND_SOLUTION_GUIDELINES.md) ·
[ADRs](docs/architecture/adr/).

## Repository layout

```
Prisma/Code/Src/CSharp/   .NET solution (01 Core · 02 Infrastructure · 03 Orchestration ·
                          04 Services · 07 UI · 08 Tests · 09 Testing)
Prisma/Code/Src/Python/   OCR pipeline, VLM extractors, synthetic document generator
Prisma/Fixtures/          Synthetic test corpora (golden cases, degraded/enhanced images)
tools/Siara.Simulator/    Stand-alone SIARA portal simulator used by the E2E tests
scripts/generators/       Synthetic corpus generators
docs/                     Architecture, ADRs, guides, public regulations
```

## Test data

All documents in this repository are **synthetic**. Names, RFCs, CURPs, account numbers and case
numbers are fictitious, and the institution used throughout the fixtures, "Banco Ejemplo", does
not exist. The regulatory PDFs under `docs/legal/regulations/` are public documents published by
the CNBV and the Diario Oficial de la Federación. The SIARA simulator is a test double — it is not
affiliated with the CNBV or the Government of Mexico.

## License

Source-available under the [PolyForm Noncommercial License 1.0.0](LICENSE.md): free for study,
research and evaluation; **commercial use requires a commercial license** — see
[`COMMERCIAL-LICENSE.md`](COMMERCIAL-LICENSE.md).

## Contact

Built by ExxerCube / IndFusion. For pilots, integration with your SIARA workflow, or the
statement-verification companion product, open an issue or reach out through GitHub.
