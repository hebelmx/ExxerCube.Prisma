<#
.SYNOPSIS
    Updates all ProjectReference paths in .csproj files after folder reorganization.

.DESCRIPTION
    After moving projects to organized folders, this script updates all
    <ProjectReference Include="..."> paths in .csproj files to reflect
    the new folder structure.

.PARAMETER DryRun
    Shows what would be changed without actually modifying files.

.EXAMPLE
    .\update_csproj_references.ps1 -DryRun
    # Shows the plan

.EXAMPLE
    .\update_csproj_references.ps1
    # Actually updates all .csproj files
#>

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

$BasePath = "Code\Src\CSharp"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "UPDATE .CSPROJ PROJECT REFERENCES" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "🔍 DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

# Build a map of project name -> new path
Write-Host "📋 BUILDING PROJECT MAP" -ForegroundColor Cyan

$ProjectMap = @{}

# Find all .csproj files and map them
Get-ChildItem -Path $BasePath -Recurse -Filter "*.csproj" | ForEach-Object {
    $csprojFile = $_
    $projectName = $csprojFile.BaseName
    $relativePath = $csprojFile.FullName.Replace((Resolve-Path $BasePath).Path + "\", "")

    $ProjectMap[$projectName] = $relativePath
}

Write-Host "   Found $($ProjectMap.Count) projects" -ForegroundColor White
Write-Host ""

# Function to calculate relative path
function Get-RelativePath {
    param(
        [string]$From,
        [string]$To
    )

    $fromParts = $From.Split('\')
    $toParts = $To.Split('\')

    # Find common path
    $commonLength = 0
    $minLength = [Math]::Min($fromParts.Length - 1, $toParts.Length - 1)  # -1 to exclude filename

    for ($i = 0; $i -lt $minLength; $i++) {
        if ($fromParts[$i] -eq $toParts[$i]) {
            $commonLength++
        }
        else {
            break
        }
    }

    # Build relative path
    $upLevels = ($fromParts.Length - 1) - $commonLength  # -1 for filename
    $relativeParts = @()

    for ($i = 0; $i -lt $upLevels; $i++) {
        $relativeParts += ".."
    }

    for ($i = $commonLength; $i -lt $toParts.Length; $i++) {
        $relativeParts += $toParts[$i]
    }

    return $relativeParts -join '\'
}

# Update all .csproj files
Write-Host "📦 UPDATING PROJECT REFERENCES" -ForegroundColor Cyan
Write-Host ""

$TotalUpdates = 0
$FilesModified = 0

Get-ChildItem -Path $BasePath -Recurse -Filter "*.csproj" | ForEach-Object {
    $csprojFile = $_
    $csprojPath = $csprojFile.FullName
    $csprojRelativePath = $csprojPath.Replace((Resolve-Path $BasePath).Path + "\", "")

    # Read file
    [xml]$csproj = Get-Content -Path $csprojPath -Encoding UTF8

    $modified = $false
    $projectUpdates = 0

    # Find all ProjectReference elements
    $projectReferences = $csproj.SelectNodes("//ProjectReference")

    foreach ($projectRef in $projectReferences) {
        $oldPath = $projectRef.Include

        # Extract project filename from old path
        $projectFileName = Split-Path -Leaf $oldPath
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFileName)

        # Find new location in map
        if ($ProjectMap.ContainsKey($projectName)) {
            $newAbsolutePath = $ProjectMap[$projectName]

            # Calculate new relative path
            $newRelativePath = Get-RelativePath -From $csprojRelativePath -To $newAbsolutePath

            if ($newRelativePath -ne $oldPath) {
                if ($DryRun) {
                    if ($projectUpdates -eq 0) {
                        Write-Host "  📄 $($csprojFile.Name)" -ForegroundColor Yellow
                    }
                    Write-Host "     $oldPath" -ForegroundColor Gray
                    Write-Host "  → $newRelativePath" -ForegroundColor Green
                }
                else {
                    if ($projectUpdates -eq 0) {
                        Write-Host "  ✓ $($csprojFile.Name)" -ForegroundColor Green
                    }
                    Write-Host "     $oldPath → $newRelativePath" -ForegroundColor Gray

                    # Update the path
                    $projectRef.Include = $newRelativePath
                    $modified = $true
                }

                $projectUpdates++
                $TotalUpdates++
            }
        }
    }

    # Save if modified
    if ($modified -and !$DryRun) {
        # Backup original
        $backupPath = $csprojPath + ".backup"
        Copy-Item -Path $csprojPath -Destination $backupPath -Force

        # Save updated file
        $csproj.Save($csprojPath)
        $FilesModified++
    }

    if ($projectUpdates -gt 0) {
        Write-Host ""
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "Mode: DRY RUN (no actual changes)" -ForegroundColor Yellow
}
else {
    Write-Host "Mode: LIVE (files modified)" -ForegroundColor Green
}

Write-Host "Total reference updates: $TotalUpdates" -ForegroundColor White
Write-Host "Files modified: $FilesModified" -ForegroundColor White

Write-Host ""

if ($DryRun) {
    Write-Host "🔄 To perform actual updates, run without -DryRun:" -ForegroundColor Yellow
    Write-Host "   .\scripts\update_csproj_references.ps1" -ForegroundColor Yellow
}
else {
    if ($TotalUpdates -gt 0) {
        Write-Host "✅ All project references updated!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📌 NEXT STEPS:" -ForegroundColor Cyan
        Write-Host "   1. Run: dotnet build Code/Src/CSharp" -ForegroundColor White
        Write-Host "   2. Fix any remaining reference errors" -ForegroundColor White
        Write-Host "   3. Run tests to verify everything works" -ForegroundColor White
    }
    else {
        Write-Host "ℹ️  No updates needed - all references already correct!" -ForegroundColor Gray
    }
}

Write-Host ""
