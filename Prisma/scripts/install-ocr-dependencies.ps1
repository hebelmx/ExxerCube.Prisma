#Requires -RunAsAdministrator
<#
.SYNOPSIS
    🧬 ExxerAI Helix OCR Dependencies Installer for Windows
    Cross-platform installation script for Tesseract OCR + OpenCV

.DESCRIPTION
    Installs Tesseract OCR with multi-language support and OpenCV libraries
    on Windows systems using Chocolatey package manager or direct downloads.

    Supports: Windows 10, Windows 11, Windows Server 2016+

.PARAMETER Languages
    Space-separated list of language codes to install.
    Default: "eng spa ita deu por rus chi_sim chi_tra jpn hin ben"

.PARAMETER Method
    Installation method: "chocolatey" or "direct"
    Default: "chocolatey" (falls back to "direct" if Chocolatey not available)

.EXAMPLE
    .\scripts\install-ocr-dependencies.ps1
    Installs with default languages using Chocolatey

.EXAMPLE
    .\scripts\install-ocr-dependencies.ps1 -Languages "eng spa ita"
    Installs only English, Spanish, and Italian language data

.EXAMPLE
    .\scripts\install-ocr-dependencies.ps1 -Method direct
    Forces direct download installation (bypasses Chocolatey)

.NOTES
    Language Codes:
      eng = English       spa = Spanish      ita = Italian
      deu = German        por = Portuguese   rus = Russian
      chi_sim = Chinese (Simplified)        chi_tra = Chinese (Traditional)
      jpn = Japanese      hin = Hindi        ben = Bengali

    Full list: https://github.com/tesseract-ocr/tessdata
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$Languages = "eng spa ita deu por rus chi_sim chi_tra jpn hin ben",

    [Parameter(Mandatory=$false)]
    [ValidateSet("chocolatey", "direct")]
    [string]$Method = "chocolatey"
)

# Stop on errors
$ErrorActionPreference = "Stop"

# Color output functions
function Write-Success { Write-Host "✓ $args" -ForegroundColor Green }
function Write-Info { Write-Host "→ $args" -ForegroundColor Cyan }
function Write-Warning { Write-Host "⚠ $args" -ForegroundColor Yellow }
function Write-Failure { Write-Host "✗ $args" -ForegroundColor Red }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ExxerAI Helix OCR Dependencies" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Detect Windows version
$osInfo = Get-CimInstance Win32_OperatingSystem
Write-Success "Detected: $($osInfo.Caption) $($osInfo.Version)"
Write-Host ""

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Failure "ERROR: This script must be run as Administrator"
    Write-Host ""
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'"
    exit 1
}

# Installation directories
$tesseractDir = "C:\Program Files\Tesseract-OCR"
$opencvDir = "C:\Program Files\opencv"
$tessdataDir = "$tesseractDir\tessdata"

# Function to check if Chocolatey is installed
function Test-Chocolatey {
    try {
        $null = Get-Command choco -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

# Function to install Chocolatey
function Install-Chocolatey {
    Write-Info "Installing Chocolatey package manager..."
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

    # Refresh environment
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

    if (Test-Chocolatey) {
        Write-Success "Chocolatey installed successfully"
    } else {
        throw "Chocolatey installation failed"
    }
}

# Function to install via Chocolatey
function Install-ViaChocolatey {
    Write-Info "Installing via Chocolatey..."

    # Install Tesseract
    Write-Info "Installing Tesseract OCR..."
    choco install tesseract -y --no-progress

    # Verify Tesseract installation
    if (-not (Test-Path $tesseractDir)) {
        throw "Tesseract installation failed - directory not found"
    }

    Write-Success "Tesseract OCR installed"

    # Install OpenCV (if available via Chocolatey)
    Write-Info "Installing OpenCV..."
    try {
        choco install opencv -y --no-progress 2>$null
        Write-Success "OpenCV installed"
    } catch {
        Write-Warning "OpenCV not available via Chocolatey (this is OK - using NuGet packages)"
    }
}

# Function to install Tesseract directly
function Install-TesseractDirect {
    Write-Info "Installing Tesseract OCR directly..."

    # Download Tesseract installer
    $installerUrl = "https://digi.bib.uni-mannheim.de/tesseract/tesseract-ocr-w64-setup-5.3.3.20231005.exe"
    $installerPath = "$env:TEMP\tesseract-installer.exe"

    Write-Info "Downloading Tesseract installer..."
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing

    Write-Info "Running Tesseract installer..."
    # Silent install with all languages
    Start-Process -FilePath $installerPath -ArgumentList "/S", "/D=$tesseractDir" -Wait -NoNewWindow

    # Cleanup
    Remove-Item $installerPath -Force

    if (Test-Path $tesseractDir) {
        Write-Success "Tesseract OCR installed to $tesseractDir"
    } else {
        throw "Tesseract installation failed"
    }
}

# Function to download Tesseract language data
function Install-LanguageData {
    param([string[]]$LangCodes)

    Write-Info "Installing Tesseract language data..."

    # Create tessdata directory if it doesn't exist
    if (-not (Test-Path $tessdataDir)) {
        New-Item -ItemType Directory -Path $tessdataDir -Force | Out-Null
    }

    # Base URL for language data (tessdata_best repository)
    $baseUrl = "https://github.com/tesseract-ocr/tessdata_best/raw/main"

    foreach ($lang in $LangCodes) {
        $trainedDataFile = "$lang.traineddata"
        $targetPath = Join-Path $tessdataDir $trainedDataFile

        # Skip if already exists
        if (Test-Path $targetPath) {
            Write-Info "Language data already exists: $lang"
            continue
        }

        try {
            Write-Info "Downloading $lang language data..."
            $url = "$baseUrl/$trainedDataFile"
            Invoke-WebRequest -Uri $url -OutFile $targetPath -UseBasicParsing
            Write-Success "Installed $lang"
        } catch {
            Write-Warning "Failed to download $lang language data (may not exist)"
        }
    }
}

# Main installation logic
try {
    # Determine installation method
    if ($Method -eq "chocolatey") {
        if (-not (Test-Chocolatey)) {
            Write-Warning "Chocolatey not found"
            $response = Read-Host "Would you like to install Chocolatey? (Y/N)"
            if ($response -eq "Y" -or $response -eq "y") {
                Install-Chocolatey
            } else {
                Write-Info "Falling back to direct installation..."
                $Method = "direct"
            }
        }

        if ($Method -eq "chocolatey") {
            Install-ViaChocolatey
        }
    }

    if ($Method -eq "direct") {
        Install-TesseractDirect
    }

    # Refresh environment variables
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")

    # Install language data
    $langArray = $Languages -split '\s+'
    Install-LanguageData -LangCodes $langArray

    # Set TESSDATA_PREFIX environment variable
    Write-Info "Setting TESSDATA_PREFIX environment variable..."
    [System.Environment]::SetEnvironmentVariable("TESSDATA_PREFIX", $tessdataDir, [System.EnvironmentVariableTarget]::Machine)
    $env:TESSDATA_PREFIX = $tessdataDir
    Write-Success "TESSDATA_PREFIX set to: $tessdataDir"

    # Add Tesseract to PATH if not already present
    $machinePath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)
    if ($machinePath -notlike "*$tesseractDir*") {
        Write-Info "Adding Tesseract to system PATH..."
        [System.Environment]::SetEnvironmentVariable(
            "Path",
            "$machinePath;$tesseractDir",
            [System.EnvironmentVariableTarget]::Machine
        )
        $env:Path += ";$tesseractDir"
        Write-Success "Tesseract added to PATH"
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Verifying Installation" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Check Tesseract
    $tesseractExe = "$tesseractDir\tesseract.exe"
    if (Test-Path $tesseractExe) {
        $version = & $tesseractExe --version 2>&1 | Select-Object -First 1
        Write-Success "Tesseract: $version"
    } else {
        Write-Failure "Tesseract: NOT FOUND"
    }

    # Check TESSDATA_PREFIX
    Write-Success "TESSDATA_PREFIX: $env:TESSDATA_PREFIX"

    # Check installed languages
    if (Test-Path $tessdataDir) {
        $installedLangs = (Get-ChildItem -Path $tessdataDir -Filter "*.traineddata").Count
        Write-Success "Installed languages: $installedLangs"
    }

    # Check OpenCV (via NuGet packages)
    Write-Info "OpenCV: Using NuGet packages (OpenCvSharp4)"

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Installation Complete! 🎉" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  1. TESSDATA_PREFIX is already set to: $tessdataDir"
    Write-Host "     (You may need to restart your terminal for changes to take effect)"
    Write-Host ""
    Write-Host "  2. Run Helix tests:"
    Write-Host "     cd code\src"
    Write-Host "     dotnet test tests\04AdapterTests\ExxerAI.Helix.Adapter.Tests\"
    Write-Host ""

} catch {
    Write-Host ""
    Write-Failure "ERROR: Installation failed"
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Manual installation instructions:"
    Write-Host "  1. Download Tesseract from: https://digi.bib.uni-mannheim.de/tesseract/"
    Write-Host "  2. Install to: C:\Program Files\Tesseract-OCR"
    Write-Host "  3. Download language data from: https://github.com/tesseract-ocr/tessdata_best"
    Write-Host "  4. Copy .traineddata files to: C:\Program Files\Tesseract-OCR\tessdata"
    Write-Host "  5. Set TESSDATA_PREFIX environment variable"
    Write-Host "  6. Add Tesseract to PATH"
    Write-Host ""
    exit 1
}
