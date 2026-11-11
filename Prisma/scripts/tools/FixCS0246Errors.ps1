# CS0246 Missing Type Error Fixer
# Systematically fixes "The type or namespace name 'X' does not exist" errors

param(
    [int]$MaxIterations = 5,
    [switch]$Verbose
)

Write-Host "🔧 CS0246 Missing Type Error Fixer" -ForegroundColor Cyan
Write-Host "Starting systematic error fixing (Max iterations: $MaxIterations)" -ForegroundColor Green

$iteration = 1
$previousErrorCount = [int]::MaxValue

while ($iteration -le $MaxIterations) {
    Write-Host "`n🔄 Iteration $iteration of $MaxIterations" -ForegroundColor Yellow
    
    # Build and capture errors
    $buildOutput = dotnet build "code/src/ExxerAI.sln" --verbosity minimal --no-restore 2>&1
    
    # Count CS0246 errors
    $cs0246Errors = ($buildOutput | Select-String "error CS0246").Count
    $totalErrors = ($buildOutput | Select-String "error CS").Count
    
    Write-Host "📊 Current status:" -ForegroundColor Cyan
    Write-Host "   CS0246 errors: $cs0246Errors" -ForegroundColor White
    Write-Host "   Total errors: $totalErrors" -ForegroundColor White
    
    # Check for improvement
    if ($totalErrors -eq 0) {
        Write-Host "🎉 SUCCESS! All errors fixed!" -ForegroundColor Green
        break
    }
    
    if ($totalErrors -ge $previousErrorCount) {
        Write-Host "⚠️ No improvement detected. Moving to next phase." -ForegroundColor Yellow
        break
    }
    
    $previousErrorCount = $totalErrors
    
    # Extract specific missing types
    $missingTypes = $buildOutput | Select-String "error CS0246.*'([^']+)'" | ForEach-Object {
        $_.Matches[0].Groups[1].Value
    } | Sort-Object | Get-Unique
    
    if ($Verbose -and $missingTypes.Count -gt 0) {
        Write-Host "🔍 Missing types detected:" -ForegroundColor Magenta
        $missingTypes | ForEach-Object { Write-Host "   - $_" -ForegroundColor Gray }
    }
    
    # Try to auto-fix some common patterns
    $fixedCount = 0
    
    # Fix common using statement issues
    $buildOutput | Select-String "error CS0246.*in (.+\.cs)" | ForEach-Object {
        $line = $_.Line
        $filePath = $_.Matches[0].Groups[1].Value
        
        if (Test-Path $filePath) {
            # Add common using statements that might be missing
            $content = Get-Content $filePath -Raw
            $modified = $false
            
            # Common missing usings for ExxerAI
            $commonUsings = @(
                "using IndQuestResults;",
                "using ExxerAI.Domain;", 
                "using ExxerAI.Application;",
                "using Microsoft.Extensions.Logging;"
            )
            
            foreach ($using in $commonUsings) {
                if ($line -match $using.Split(';')[0].Replace('using ', '') -and $content -notmatch [regex]::Escape($using)) {
                    $content = $using + "`n" + $content
                    $modified = $true
                    $fixedCount++
                    if ($Verbose) {
                        Write-Host "   ✅ Added $using to $filePath" -ForegroundColor Green
                    }
                }
            }
            
            if ($modified) {
                Set-Content $filePath -Value $content -NoNewline
            }
        }
    }
    
    if ($fixedCount -gt 0) {
        Write-Host "🔧 Applied $fixedCount automatic fixes" -ForegroundColor Green
    } else {
        Write-Host "⏸️ No automatic fixes available. Manual intervention may be needed." -ForegroundColor Yellow
        break
    }
    
    $iteration++
    Start-Sleep -Seconds 2
}

Write-Host "`n📋 Final Summary:" -ForegroundColor Cyan
Write-Host "   Iterations completed: $($iteration - 1)" -ForegroundColor White
Write-Host "   Final error count: $totalErrors" -ForegroundColor White

if ($totalErrors -eq 0) {
    Write-Host "🎉 Build successful!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "⚠️ $totalErrors errors remain. Manual fixing needed." -ForegroundColor Yellow
    exit 1
}