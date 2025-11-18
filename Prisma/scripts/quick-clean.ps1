# Quick Clean Script
Write-Host "Quick cleaning build artifacts..." -ForegroundColor Cyan

# Navigate to solution directory
Set-Location "F:\Dynamic\ExxerAi\ExxerAI\code\src"

# Clean and clear caches
dotnet clean
dotnet nuget locals all --clear

# Remove all bin and obj folders
Get-ChildItem -Recurse -Directory -Include bin,obj,.vs | Remove-Item -Recurse -Force

Write-Host "Done! Restart Visual Studio and run 'dotnet restore'" -ForegroundColor Green