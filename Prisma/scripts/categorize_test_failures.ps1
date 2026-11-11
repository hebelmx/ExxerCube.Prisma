#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Categorize remaining test failures by type
.DESCRIPTION
    Analyzes PrismaFailedTests.txt and categorizes failures into:
    - Outdated Expectations (test assertions need updating)
    - Test Bugs (incorrect test logic)
    - Production Bugs (actual implementation issues)
    - Missing Features (not yet implemented)
#>

$reportPath = "PrismaFailedTests.txt"

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "TEST FAILURE CATEGORIZATION" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $reportPath)) {
    Write-Host "Error: $reportPath not found" -ForegroundColor Red
    exit 1
}

$content = Get-Content $reportPath -Raw

# Extract all test method names with their errors
$testPattern = '(\w+)\s+\[\d+:\d+\.\d+\]\s+Failed:\s+(.+?)(?=\n\s+\w+\s+\[|\Z)'
$matches = [regex]::Matches($content, $testPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)

# Categories
$categories = @{
    "Outdated Expectations" = @()
    "Test Bugs" = @()
    "Production Bugs" = @()
    "Timeout/Environment" = @()
}

foreach ($match in $matches) {
    $testName = $match.Groups[1].Value
    $error = $match.Groups[2].Value

    # Categorize based on error patterns
    if ($error -match "should be less than|should be greater than|ShouldBe.*but was|expected.*but found") {
        if ($error -match "similarity|threshold|percentage|score") {
            $categories["Outdated Expectations"] += [PSCustomObject]@{
                Test = $testName
                Reason = "Threshold/similarity expectation mismatch"
                Details = ($error -split "`n")[0..2] -join " | "
            }
        }
        elseif ($error -match "cannot be empty|match the equivalent") {
            $categories["Outdated Expectations"] += [PSCustomObject]@{
                Test = $testName
                Reason = "Error message format changed"
                Details = ($error -split "`n")[0..1] -join " | "
            }
        }
        elseif ($error -match "HasKeyValuePairs|HasStructuredFormat") {
            $categories["Production Bugs"] += [PSCustomObject]@{
                Test = $testName
                Reason = "Feature not detecting expected structures"
                Details = ($error -split "`n")[0..1] -join " | "
            }
        }
        else {
            $categories["Test Bugs"] += [PSCustomObject]@{
                Test = $testName
                Reason = "Assertion mismatch"
                Details = ($error -split "`n")[0..1] -join " | "
            }
        }
    }
    elseif ($error -match "timeout|timed out|did not complete") {
        $categories["Timeout/Environment"] += [PSCustomObject]@{
            Test = $testName
            Reason = "Timeout or environment issue"
            Details = ($error -split "`n")[0]
        }
    }
    elseif ($error -match "not implemented|NotImplementedException") {
        $categories["Production Bugs"] += [PSCustomObject]@{
            Test = $testName
            Reason = "Feature not yet implemented"
            Details = "NotImplementedException"
        }
    }
    else {
        $categories["Test Bugs"] += [PSCustomObject]@{
            Test = $testName
            Reason = "Unknown failure pattern"
            Details = ($error -split "`n")[0]
        }
    }
}

# Display results
foreach ($category in $categories.Keys | Sort-Object) {
    $items = $categories[$category]
    if ($items.Count -gt 0) {
        Write-Host ""
        Write-Host "━━━ $category ($($items.Count) tests) ━━━" -ForegroundColor Yellow
        Write-Host ""

        foreach ($item in $items) {
            Write-Host "  📌 $($item.Test)" -ForegroundColor White
            Write-Host "     → $($item.Reason)" -ForegroundColor Gray
            if ($item.Details) {
                $shortDetails = $item.Details.Substring(0, [Math]::Min(100, $item.Details.Length))
                Write-Host "     $shortDetails..." -ForegroundColor DarkGray
            }
            Write-Host ""
        }
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Outdated Expectations: $($categories['Outdated Expectations'].Count)" -ForegroundColor Yellow
Write-Host "Production Bugs: $($categories['Production Bugs'].Count)" -ForegroundColor Red
Write-Host "Test Bugs: $($categories['Test Bugs'].Count)" -ForegroundColor Magenta
Write-Host "Timeout/Environment: $($categories['Timeout/Environment'].Count)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total: $(($categories.Values | Measure-Object -Property Count -Sum).Sum) categorized failures" -ForegroundColor White
Write-Host ""
