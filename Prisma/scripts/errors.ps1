# Quick error navigation helper
# Shows errors with line numbers and lets you jump to them

$errors = dotnet build --no-restore 2>&1 | Select-String -Pattern "error CS"

if ($errors.Count -eq 0) {
    Write-Host "✅ No build errors!" -ForegroundColor Green
    exit
}

Write-Host "❌ Found $($errors.Count) errors:" -ForegroundColor Red
Write-Host ""

$i = 1
$errorList = @()

foreach ($error in $errors) {
    # Parse error format: path\file.cs(line,col): error CS####: message
    if ($error -match '(.+\.cs)\((\d+),\d+\):\s+error\s+(CS\d+):\s+(.+)') {
        $file = $Matches[1]
        $line = $Matches[2]
        $code = $Matches[3]
        $msg = $Matches[4]
        
        $errorList += @{
            File = $file
            Line = $line
            Code = $code
            Message = $msg
        }
        
        Write-Host "[$i] " -NoNewline -ForegroundColor Yellow
        Write-Host "$code " -NoNewline -ForegroundColor Red
        Write-Host "Line $line " -NoNewline -ForegroundColor Gray
        Write-Host $(Split-Path $file -Leaf) -ForegroundColor Cyan
        Write-Host "    $msg" -ForegroundColor White
        Write-Host ""
        
        $i++
    }
}

Write-Host "Enter error number to copy path (or Q to quit): " -NoNewline -ForegroundColor Green
$choice = Read-Host

if ($choice -ne 'Q' -and $choice -match '^\d+$') {
    $idx = [int]$choice - 1
    if ($idx -ge 0 -and $idx -lt $errorList.Count) {
        $selected = $errorList[$idx]
        $fullPath = "$($selected.File):$($selected.Line)"
        $fullPath | Set-Clipboard
        Write-Host ""
        Write-Host "📋 Copied to clipboard: $fullPath" -ForegroundColor Green
        Write-Host "You can paste this in VS Code to jump to the error" -ForegroundColor Gray
    }
}