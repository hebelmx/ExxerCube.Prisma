# PowerShell Script to Generate Bulk CNBV Fixtures with Low System Load

# --- Configuration ---
$TotalDocuments = 500
$BatchSize = 20
$DelayBetweenBatches = 10 # Seconds
$OutputDirectory = "bulk_generated_documents"
$GeneratorScript = "Prisma/Fixtures/generators/AAAV2_refactored/main_generator.py"

# --- Generation Parameters (for diversity) ---
$ChaosLevels = @("none", "low", "medium", "high")
$RequirementTypes = @("fiscal", "judicial", "pld", "aseguramiento", "informacion")
$Authorities = @("IMSS", "SAT", "UIF", "FGR", "SEIDO", "PJF", "INFONAVIT", "SHCP", "CONDUSEF")
$Formats = @("html") # Focused on HTML for E2E tests

# --- Script Logic ---
$NumberOfBatches = [Math]::Ceiling($TotalDocuments / $BatchSize)

Write-Host "🚀 Starting bulk generation of $TotalDocuments documents in $NumberOfBatches batches."
Write-Host "----------------------------------------------------------------"

# Create output directory if it doesn't exist
if (-not (Test-Path -Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

for ($i = 1; $i -le $NumberOfBatches; $i++) {
    # --- Randomize parameters for this batch ---
    $RandomChaos = $ChaosLevels | Get-Random
    $RandomType = $RequirementTypes | Get-Random
    $RandomAuthority = $Authorities | Get-Random

    Write-Host "🔥 Batch $i / $NumberOfBatches: Generating $BatchSize documents..."
    Write-Host "   - Chaos Level: $RandomChaos"
    Write-Host "   - Requirement Type: $RandomType"
    Write-Host "   - Authority: $RandomAuthority"
    Write-Host "   - Formats: $($Formats -join ', ')"

    # --- Construct and execute the command ---
    $command = "python $GeneratorScript --count $BatchSize --output $OutputDirectory --chaos $RandomChaos --types $RandomType --authority $RandomAuthority --formats $($Formats -join ' ')"
    
    try {
        Invoke-Expression -Command $command
        Write-Host "✅ Batch $i completed successfully."
    }
    catch {
        Write-Host "❌ Error during batch $i. See details below:"
        Write-Host $_.Exception.Message
    }

    # --- Pause between batches to reduce system load ---
    if ($i -lt $NumberOfBatches) {
        Write-Host "💤 Pausing for $DelayBetweenBatches seconds to reduce system load..."
        Start-Sleep -Seconds $DelayBetweenBatches
    }
    Write-Host "----------------------------------------------------------------"
}

Write-Host "🎉 Bulk generation complete. All documents are in the '$OutputDirectory' folder."
