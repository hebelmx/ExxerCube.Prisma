# ExxerAI Development Helper - Makes console development easier
# Usage: .\dev.ps1 [command] [args]

param(
    [string]$Command = "help",
    [string]$Project = "",
    [string]$Filter = ""
)

$Root = "F:\Dynamic\ExxerAi\ExxerAI\code\src"
Set-Location $Root

# Color coding for output
function Write-Success($msg) { Write-Host $msg -ForegroundColor Green }
function Write-Error($msg) { Write-Host $msg -ForegroundColor Red }
function Write-Info($msg) { Write-Host $msg -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host $msg -ForegroundColor Yellow }

# Main commands
switch ($Command.ToLower()) {
    "b" { # Build
        Write-Info "🔨 Building solution..."
        $result = dotnet build --no-restore 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "✅ Build succeeded!"
        } else {
            Write-Error "❌ Build failed!"
            $result | Select-String -Pattern "error" | ForEach-Object { Write-Error $_ }
        }
    }
    
    "t" { # Test
        if ($Project) {
            Write-Info "🧪 Testing $Project..."
            dotnet test "*$Project*" --no-build
        } else {
            Write-Info "🧪 Running all tests..."
            dotnet test --no-build
        }
    }
    
    "w" { # Watch
        Write-Info "👁️ Watching for changes..."
        dotnet watch build
    }
    
    "c" { # Clean
        Write-Info "🧹 Cleaning solution..."
        dotnet clean
        Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force
        Write-Success "✅ Cleaned!"
    }
    
    "r" { # Restore
        Write-Info "📦 Restoring packages..."
        dotnet restore
    }
    
    "f" { # Find errors
        Write-Info "🔍 Finding errors..."
        $errors = dotnet build --no-restore 2>&1 | Select-String -Pattern "error CS"
        if ($errors) {
            $errors | ForEach-Object { Write-Error $_ }
            Write-Warn "Found $($errors.Count) errors"
        } else {
            Write-Success "✅ No errors found!"
        }
    }
    
    "grep" { # Search code
        if ($Filter) {
            Write-Info "🔎 Searching for '$Filter'..."
            Get-ChildItem -Recurse -Filter "*.cs" | Select-String -Pattern $Filter | Format-Table -Property Line,Filename -AutoSize
        } else {
            Write-Warn "Usage: .\dev.ps1 grep <search-pattern>"
        }
    }
    
    "proj" { # List projects
        Write-Info "📁 Projects in solution:"
        Get-ChildItem -Recurse -Filter "*.csproj" | ForEach-Object {
            $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            $path = $_.DirectoryName.Replace($Root, ".")
            Write-Host "  $name `t-> $path" -ForegroundColor Gray
        }
    }
    
    "errors" { # Show current errors
        if (Test-Path "F:\Dynamic\ExxerAi\ExxerAI\Errors") {
            Write-Info "📋 Error files:"
            Get-ChildItem "F:\Dynamic\ExxerAi\ExxerAI\Errors\*.txt" | ForEach-Object {
                $count = (Get-Content $_.FullName | Measure-Object -Line).Lines - 1
                Write-Host "  $($_.Name): $count errors" -ForegroundColor Yellow
            }
        }
    }
    
    default {
        Write-Info @"
ExxerAI Development Helper Commands:
===================================
  b          - Build solution
  t [proj]   - Test all or specific project
  w          - Watch mode (auto-rebuild)
  c          - Clean solution
  r          - Restore packages
  f          - Find build errors
  grep <pat> - Search in .cs files
  proj       - List all projects
  errors     - Show error summary

Examples:
  .\dev.ps1 b                    # Build
  .\dev.ps1 t Domain             # Test Domain project
  .\dev.ps1 grep "Result<"       # Search for Result< in code
"@
    }
}