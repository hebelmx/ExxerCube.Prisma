# Disable everything that makes VS freeze
# Because apparently "Open File" is too complex for a 2024 IDE

Write-Host "Applying Visual Studio 'Please Just Work' settings..." -ForegroundColor Cyan

# Create a .editorconfig to disable ALL the things
$editorConfig = @'
root = true

[*]
# Disable all code analysis
dotnet_analyzer_diagnostic.severity = none
dotnet_code_quality.api_surface = all
dotnet_code_quality.enable = false

# Disable all style rules
dotnet_diagnostic.IDE0001.severity = none
dotnet_diagnostic.IDE0002.severity = none
dotnet_diagnostic.IDE0003.severity = none
dotnet_diagnostic.IDE0004.severity = none
dotnet_diagnostic.IDE0005.severity = none
# ... (there are like 200 of these)

# Disable refactoring suggestions
dotnet_diagnostic.IDE0007_gen.severity = none
dotnet_diagnostic.IDE0007.severity = none

# Turn off everything
generated_code = true
'@

$editorConfig | Out-File -FilePath "F:\Dynamic\ExxerAi\ExxerAI\code\src\.editorconfig" -Encoding UTF8

# Create settings to disable Roslyn features
$settings = @{
    "RoslynExtensionsOptions": @{
        "EnableAnalyzersSupport": $false
        "EnableCodeFixes": $false
        "EnableRefactorings": $false
    }
    "TextEditor": @{
        "CodeLens": $false
        "SuggestionsEnabled": $false
    }
    "Copilot": @{
        "Enable": $false
        "ShowSuggestions": $false
    }
}

$settings | ConvertTo-Json | Out-File -FilePath "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0_*\Settings\CurrentSettings.vssettings" -Encoding UTF8

Write-Host @"

Done! Also try these manual steps in VS:

Tools → Options:
✓ Text Editor → All Languages → CodeLens → DISABLE
✓ Text Editor → All Languages → Enable mouse click to Go To Definition → DISABLE  
✓ Text Editor → C# → Advanced → Enable full solution analysis → DISABLE
✓ IntelliCode → General → C# suggestions → DISABLE
✓ Projects and Solutions → General → Track Active Item → DISABLE

If it's still freezing, just use VS Code:
code .

"@ -ForegroundColor Yellow