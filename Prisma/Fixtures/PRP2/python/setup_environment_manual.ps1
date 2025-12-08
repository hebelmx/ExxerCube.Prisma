# Manual Python Environment Setup for VEC Extraction
# This script sets up the Python environment BEFORE CSnakes runs
# Following the proven reliable approach from GOT-OCR2

param(
    [string]$PythonVersion = "3.13.1",
    [string]$VenvPath = ".venv_vec",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host "=== VEC Extraction - Manual Python Environment Setup ===" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$ScriptDir = $PSScriptRoot
$RequirementsFile = Join-Path $ScriptDir "requirements.txt"

Write-Host "Script directory: $ScriptDir" -ForegroundColor Yellow
Write-Host "Requirements file: $RequirementsFile" -ForegroundColor Yellow
Write-Host ""

# Step 1: Check if Python is installed
Write-Host "[1/6] Checking Python installation..." -ForegroundColor Green

$PythonCmd = $null
$PythonExe = $null

# Try python3 first, then python
try {
    $PythonCmd = Get-Command python3 -ErrorAction SilentlyContinue
    if ($null -eq $PythonCmd) {
        $PythonCmd = Get-Command python -ErrorAction SilentlyContinue
    }

    if ($null -ne $PythonCmd) {
        $PythonExe = $PythonCmd.Source
        $InstalledVersion = & $PythonExe --version 2>&1
        Write-Host "  ✓ Found: $InstalledVersion at $PythonExe" -ForegroundColor Green
    }
    else {
        throw "Python not found in PATH"
    }
}
catch {
    Write-Host "  ✗ Python not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Python 3.13.1 from:" -ForegroundColor Yellow
    Write-Host "  https://www.python.org/downloads/" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or download Python redistributable:" -ForegroundColor Yellow
    Write-Host "  https://github.com/indygreg/python-build-standalone/releases" -ForegroundColor Yellow
    exit 1
}

# Step 2: Create virtual environment
Write-Host ""
Write-Host "[2/6] Creating virtual environment..." -ForegroundColor Green

$VenvFullPath = Join-Path $ScriptDir $VenvPath

if (Test-Path $VenvFullPath) {
    if ($Force) {
        Write-Host "  ! Removing existing venv (Force mode)..." -ForegroundColor Yellow
        Remove-Item -Path $VenvFullPath -Recurse -Force
    }
    else {
        Write-Host "  ✓ Virtual environment already exists at: $VenvFullPath" -ForegroundColor Green
        Write-Host "  Use -Force to recreate" -ForegroundColor Yellow
    }
}

if (-not (Test-Path $VenvFullPath)) {
    Write-Host "  Creating venv at: $VenvFullPath" -ForegroundColor Yellow
    & $PythonExe -m venv $VenvFullPath
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Failed to create virtual environment" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Virtual environment created" -ForegroundColor Green
}

# Step 3: Activate virtual environment and get pip
Write-Host ""
Write-Host "[3/6] Activating virtual environment..." -ForegroundColor Green

$VenvPython = Join-Path $VenvFullPath "Scripts\python.exe"
$VenvPip = Join-Path $VenvFullPath "Scripts\pip.exe"

if (-not (Test-Path $VenvPython)) {
    Write-Host "  ✗ Virtual environment Python not found at: $VenvPython" -ForegroundColor Red
    exit 1
}

Write-Host "  ✓ Venv Python: $VenvPython" -ForegroundColor Green
Write-Host "  ✓ Venv Pip: $VenvPip" -ForegroundColor Green

# Step 4: Upgrade pip, setuptools, wheel
Write-Host ""
Write-Host "[4/6] Upgrading pip, setuptools, wheel..." -ForegroundColor Green

& $VenvPython -m pip install --upgrade pip setuptools wheel --quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ✗ Failed to upgrade pip" -ForegroundColor Red
    exit 1
}

$PipVersion = & $VenvPip --version
Write-Host "  ✓ $PipVersion" -ForegroundColor Green

# Step 5: Install requirements with EXACT versions
Write-Host ""
Write-Host "[5/6] Installing requirements with EXACT versions..." -ForegroundColor Green
Write-Host "  This may take 10-30 minutes (downloading PyTorch ~2GB + transformers)" -ForegroundColor Yellow
Write-Host ""

if (-not (Test-Path $RequirementsFile)) {
    Write-Host "  ✗ Requirements file not found: $RequirementsFile" -ForegroundColor Red
    exit 1
}

Write-Host "  Installing from: $RequirementsFile" -ForegroundColor Yellow

# Install with exact versions (no upgrades, no dependencies resolution conflicts)
& $VenvPip install -r $RequirementsFile --no-deps --no-cache-dir

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  ✗ Installation failed!" -ForegroundColor Red
    Write-Host "  Trying again with dependency resolution..." -ForegroundColor Yellow
    Write-Host ""

    # Retry without --no-deps (let pip resolve dependencies)
    & $VenvPip install -r $RequirementsFile

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Installation failed again!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please check:" -ForegroundColor Yellow
        Write-Host "  1. Internet connection" -ForegroundColor Yellow
        Write-Host "  2. PyPI availability" -ForegroundColor Yellow
        Write-Host "  3. Disk space (requires ~10GB)" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "  ✓ Requirements installed successfully" -ForegroundColor Green

# Step 6: Verify installation
Write-Host ""
Write-Host "[6/6] Verifying installation..." -ForegroundColor Green

$VerifyScript = @"
import sys
print(f"Python: {sys.version}")

# Check critical packages
packages_to_check = [
    'torch',
    'transformers',
    'pydantic',
    'pdfplumber',
    'PIL',
    'cv2'
]

print("\nInstalled packages:")
for pkg_name in packages_to_check:
    try:
        if pkg_name == 'PIL':
            import PIL
            pkg = PIL
        elif pkg_name == 'cv2':
            import cv2
            pkg = cv2
        else:
            pkg = __import__(pkg_name)

        version = getattr(pkg, '__version__', 'unknown')
        print(f"  ✓ {pkg_name}: {version}")
    except ImportError as e:
        print(f"  ✗ {pkg_name}: NOT INSTALLED")
        sys.exit(1)

print("\n✓ All critical packages verified!")
"@

$TempVerifyScript = Join-Path $env:TEMP "verify_vec_env.py"
$VerifyScript | Out-File -FilePath $TempVerifyScript -Encoding UTF8

& $VenvPython $TempVerifyScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  ✗ Verification failed!" -ForegroundColor Red
    exit 1
}

Remove-Item $TempVerifyScript -Force

# Success summary
Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Virtual environment created at: $VenvFullPath" -ForegroundColor Cyan
Write-Host "Python executable: $VenvPython" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run the console demo:" -ForegroundColor Yellow
Write-Host "     cd Prisma\Code\Src\CSharp\05-ConsoleApp\ConsoleApp.VecExtractionDemo" -ForegroundColor White
Write-Host "     dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "  2. Or activate the environment manually:" -ForegroundColor Yellow
Write-Host "     $VenvFullPath\Scripts\Activate.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Environment is ready for CSnakes integration!" -ForegroundColor Green
