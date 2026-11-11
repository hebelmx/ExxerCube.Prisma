# Visual Studio Workarounds for .NET 10 Preview

Write-Host "Applying Visual Studio workarounds for .NET 10..." -ForegroundColor Cyan

# 1. Force MSBuild to use newer version
$env:MSBuildSDKsPath = "C:\Program Files\dotnet\sdk\10.0.100-rc.2.25502.107\Sdks"

# 2. Disable certain analyzers that cause issues
Write-Host "Creating Directory.Build.props to disable problematic analyzers..." -ForegroundColor Yellow

$directoryBuildProps = @'
<Project>
  <PropertyGroup>
    <!-- Disable analyzers that cause VS issues -->
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
    <AnalysisMode>None</AnalysisMode>
    
    <!-- Reduce design-time builds -->
    <DesignTimeBuild>false</DesignTimeBuild>
    <SkipCompilerExecution>true</SkipCompilerExecution>
    
    <!-- Disable Source Link for local builds -->
    <EnableSourceLink>false</EnableSourceLink>
    <DeterministicSourcePaths>false</DeterministicSourcePaths>
  </PropertyGroup>
</Project>
'@

# Save to solution root
$directoryBuildProps | Out-File -FilePath "F:\Dynamic\ExxerAi\ExxerAI\code\src\Directory.Build.props" -Encoding UTF8

# 3. Create VS-specific launch settings
Write-Host "Creating optimized launchSettings.json..." -ForegroundColor Yellow

$launchSettings = @'
{
  "profiles": {
    "Minimal": {
      "commandName": "Project",
      "launchBrowser": false,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1"
      }
    }
  }
}
'@

# 4. Create a minimal solution filter to reduce VS load
Write-Host "Creating solution filter for better performance..." -ForegroundColor Yellow

$slnFilter = @'
{
  "solution": {
    "path": "ExxerAI.sln",
    "projects": [
      "Core\\ExxerAI.Domain\\ExxerAI.Domain.csproj",
      "Core\\ExxerAI.Application\\ExxerAI.Application.csproj",
      "Infrastructure\\ExxerAI.Axis\\ExxerAI.Axis.csproj"
    ]
  }
}
'@

$slnFilter | Out-File -FilePath "F:\Dynamic\ExxerAi\ExxerAI\code\src\ExxerAI.minimal.slnf" -Encoding UTF8

Write-Host "`nWorkarounds applied!" -ForegroundColor Green
Write-Host "`nRecommendations:" -ForegroundColor Yellow
Write-Host "1. Use 'ExxerAI.minimal.slnf' instead of full solution" -ForegroundColor White
Write-Host "2. Disable 'Enable Diagnostic Tools while debugging' in VS" -ForegroundColor White
Write-Host "3. Turn off 'Enable JavaScript debugging for ASP.NET' in VS" -ForegroundColor White
Write-Host "4. Consider using VS Code or command line for heavy operations" -ForegroundColor White