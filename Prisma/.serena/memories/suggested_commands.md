# Suggested Commands
- Restore/build/test the main solution from `Code/Src/CSharp`: `dotnet restore ExxerCube.Prisma.sln`, `dotnet build ExxerCube.Prisma.sln`, `dotnet test ExxerCube.Prisma.sln`.
- Run the Blazor UI locally: `dotnet run --project UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj` (and optionally `dotnet watch run` while iterating).
- Launch console/worker pipelines as needed, e.g. `dotnet run --project Application/ExxerCube.Prisma.Ocr.Console` (adjust to target project path).
- Mutation testing: `dotnet tool run dotnet-stryker --config-file stryker-config.json` inside `Code/Src/CSharp` (config references `ExxerCube.Prisma.sln`).
- Repo-wide code search relies on ripgrep: `rg <pattern> -g '*.cs' Code/Src/CSharp`; Windows shell defaults to PowerShell so `Get-ChildItem` (`ls`), `Set-Location` (`cd`), and `Get-Content` (`cat`) are the usual navigation tools.
- Utility scripts (PowerShell + Python) live in `/scripts`; e.g., `pwsh scripts/dev.ps1` bootstraps the dev workflow, and `pwsh scripts/Clear-VSCache.ps1` cleans stale VS caches when builds misbehave.