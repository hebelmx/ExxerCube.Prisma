# Task Completion Checklist
1. Build + unit/integration tests: run `dotnet build ExxerCube.Prisma.sln` and `dotnet test ExxerCube.Prisma.sln` from `Code/Src/CSharp`; add targeted project runs (e.g., UI or Tests.*) when scope is narrow.
2. UI work: ensure the Blazor site launches via `dotnet run --project UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj` and exercise the changed components manually.
3. Keep warnings at zero (warnings-as-errors on) and align with `.editorconfig`/nullable rules; run `dotnet format` on touched projects if spacing/naming changed.
4. Update docs (README, IMPLEMENTATION_SUMMARY, etc.) plus fixtures when behavior or configuration shifts.
5. Optional but encouraged: execute Stryker (`dotnet tool run dotnet-stryker`) for significant domain logic changes to keep mutation score healthy.
6. Clean up dev artifacts (bin/obj/test_output) or use helper scripts (e.g., `pwsh scripts/quick-clean.ps1`) before handing off the change.