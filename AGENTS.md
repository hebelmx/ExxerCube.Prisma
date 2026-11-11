# Repository Guidelines

## Project Structure & Module Organization
- Solution: `Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln` groups Domain, Application, Infrastructure (Database, Extraction, Classification, BrowserAutomation, FileStorage), UI (Blazor), and Testing projects.  
- Core domain models, SmartEnums, and value objects live under `Prisma/Code/Src/CSharp/Domain`.  
- Application services (ingestion, extraction, decision, export) are in `Prisma/Code/Src/CSharp/Application`.  
- Infrastructure implementations sit in `Prisma/Code/Src/CSharp/Infrastructure.*`; EF configs are under `Infrastructure.Database/EntityFramework`.  
- Tests: unit/integration under `Tests.*` folders; system/E2E under `Tests.System.*` and `Tests.EndToEnd`. Fixtures for XML/PDF live in `Prisma/Fixtures`.

## Build, Test, and Development Commands
- Build all projects: `dotnet build Prisma/Code/Src/CSharp/ExxerCube.Prisma.sln -warnaserror`.  
- Run fast app-layer tests: `dotnet test Prisma/Code/Src/CSharp/Tests.Application/ExxerCube.Prisma.Tests.Application.csproj`.  
- Run system XML extraction tests: `dotnet test Prisma/Code/Src/CSharp/Tests.Infrastructure.XmlExtraction/ExxerCube.Prisma.Tests.System.XmlExtraction.csproj`.  
- For slow E2E/Playwright runs, use filters: `dotnet test ... --filter "Category=E2E"`.

## Coding Style & Naming Conventions
- C# 10/NET 10; warnings-as-errors enforced. Prefer explicit null checks instead of nullable annotations for safety.  
- SmartEnums derive from `EnumModel`; always add EF value converters and value comparers in entity configurations.  
- Use PascalCase for types and properties, camelCase for locals/params, and clear display names in SmartEnums.  
- Keep methods small and side-effect aware; favor pure functions and observable patterns over eventing.

## Testing Guidelines
- Follow ITTDD/TDD: add/extend tests alongside changes.  
- Use real fixtures from `Prisma/Fixtures` when possible; add negative mirrors (missing/blank fields) for reconciliation scenarios.  
- System tests should exercise the full pipeline (XML/PDF → extraction → reconciliation); unit tests should isolate services with substitutes.  
- Name tests as `Method_Scenario_Expectation`; keep logs via the provided XUnit logger helpers for diagnosability.

## Commit & Pull Request Guidelines
- Commit messages: present tense, scoped (e.g., `Add EF converters for ReviewReason`).  
- PRs need: summary of behavior change, linked requirement/ticket, screenshots for UI updates, and test evidence (`dotnet test ...`).  
- Keep changes small and reversible; avoid solution churn (no adding/removing projects without approval).  
- For breaking migrations or SmartEnum changes, call out downstream impacts (DB mappings, JSON serializers, cache entries) and propose rollout steps.

## Security & Configuration Notes
- No secrets in code; use environment variables/appsettings for credentials.  
- EF InMemory requires explicit converters for SmartEnums; ensure production configurations stay in sync.  
- Keep reconciliation logic tolerant: missing XML/PDF fields should raise warnings/flags, not stop the pipeline.
