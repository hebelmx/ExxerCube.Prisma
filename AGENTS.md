# Repository Guidelines

## Project Structure & Module Organization
- Core C# solution lives in `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln` with Domain, Application, and Infrastructure projects; tests sit under the adjacent `Tests.*` projects plus Playwright E2E suites.
- Python tooling and extractors reside in `Prisma/Code/Src/Python` (see `prisma-ai-extractors`, `prisma-ocr-pipeline`, and `Prisma-dumy-generator-AAA`); each app has its own `src/` and `tests/`.
- Root-level automation and docs: `docs/`, `deployment/`, `Fixtures/`, and helper scripts such as `commit-all-changes.ps1` and `migrate-tests.ps1`.
- Front-end style Playwright examples live in `tests/` at the repo root and use `playwright.config.ts`.

## Build, Test, and Development Commands
- `cd Prisma/Code/Src/CSharp && dotnet restore` – restore NuGet packages.
- `cd Prisma/Code/Src/CSharp && dotnet build ExxerCube.Prisma.sln` – compile all projects (warnings are treated seriously).
- `cd Prisma/Code/Src/CSharp && dotnet test ExxerCube.Prisma.sln` – run unit, integration, architecture, and Playwright-backed E2E tests.
- `npx playwright test` from repo root – executes the sample browser tests in `tests/` using `@playwright/test`.
- Python extractors: `cd Prisma/Code/Src/Python/prisma-ai-extractors && pytest -q tests` for model and utility coverage; adapt the path per app.

## Coding Style & Naming Conventions
- C#: keep hexagonal boundaries (Domain contracts, Application orchestration, Infrastructure adapters); prefer dependency injection and adapter pattern used for Python integration. Use PascalCase for types/namespaces, camelCase for locals/parameters, and 4-space indentation.
- Python: snake_case for functions/modules, PascalCase for classes, and include type hints where possible; keep CLIs thin and delegate to services.
- Front-end/E2E: place new Playwright specs beside similar flows, favor descriptive test names (`<component>_<behavior>_<expectation>`).

## Testing Guidelines
- Target ≥80% coverage on critical C# units (see architecture and adapter tests in `Tests.*` projects); add focused unit tests before integration/E2E.
- Prefer fast `dotnet test --filter "<TraitExpression>"` during development; keep Playwright tests deterministic with explicit waits and encoded data URLs.
- Python apps: add pytest cases alongside modules; include fixtures for OCR samples where applicable.
- Commit only when the full suite (or scoped filters) is green; note commands run in the PR description.

## Commit & Pull Request Guidelines
- Follow conventional prefixes seen in history (`feat:`, `fix:`, `chore:`, `docs:`) with a concise, outcome-focused subject.
- In bodies, summarize key architectural impacts (e.g., adapter moves, boundary enforcement) and list major test commands executed.
- PRs should link issues/ADR references, describe scope and risk, and attach evidence (test output, architecture screenshots if relevant). Call out changes to pipelines, secrets, or data contracts explicitly.

## Security & Configuration Tips
- Do not commit secrets; use environment variables or local user secrets for cloud keys and OCR credentials. Check `docs/` and `deployment/` notes before enabling external services.
- Large fixture and model files live under `Fixtures/` and `bulk_generated_documents_*`; avoid duplicating them—reference existing assets where possible.
- Persist proposals: save all review findings and code/refactor proposals as Markdown in `docs/` (clear filenames) so they survive sessions and can be reviewed asynchronously.
