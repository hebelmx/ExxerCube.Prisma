# ExxerCube Prisma - Certificate Installation Script (Windows)
# Run this ONCE per production machine BEFORE first deployment

param(
    [string]$CertPath = ".\certs\prisma.pfx",
    [string]$CACertPath = ".\certs\ca.crt",
    [string]$Password = ""
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ExxerCube Prisma Certificate Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/5] Checking for existing certificates..." -ForegroundColor Yellow

# Check if CA certificate already installed
$existingCA = Get-ChildItem Cert:\LocalMachine\Root | Where-Object {$_.Subject -like "*ExxerCube Internal CA*"}
if ($existingCA) {
    Write-Host "  ✓ CA certificate already installed" -ForegroundColor Green
    Write-Host "    Subject: $($existingCA.Subject)" -ForegroundColor Gray
    Write-Host "    Expires: $($existingCA.NotAfter)" -ForegroundColor Gray
} else {
    Write-Host "  ✗ CA certificate NOT installed" -ForegroundColor Red
}

# Check if application certificate already installed
$existingAppCert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"}
if ($existingAppCert) {
    Write-Host "  ✓ Application certificate already installed" -ForegroundColor Green
    Write-Host "    Subject: $($existingAppCert.Subject)" -ForegroundColor Gray
    Write-Host "    Expires: $($existingAppCert.NotAfter)" -ForegroundColor Gray

    # Check if expiring soon
    $daysUntilExpiry = ($existingAppCert.NotAfter - (Get-Date)).Days
    if ($daysUntilExpiry -lt 30) {
        Write-Host "  ⚠ WARNING: Certificate expires in $daysUntilExpiry days!" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ✗ Application certificate NOT installed" -ForegroundColor Red
}

Write-Host ""

# If both installed, ask if should reinstall
if ($existingCA -and $existingAppCert) {
    $reinstall = Read-Host "Certificates already installed. Reinstall? (y/N)"
    if ($reinstall -ne "y" -and $reinstall -ne "Y") {
        Write-Host "Skipping installation." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "[2/5] Validating certificate files..." -ForegroundColor Yellow

# Check if CA certificate file exists
if (-not (Test-Path $CACertPath)) {
    Write-Host "  ERROR: CA certificate not found at: $CACertPath" -ForegroundColor Red
    Write-Host "  Expected file: ca.crt" -ForegroundColor Yellow
    exit 1
}
Write-Host "  ✓ CA certificate file found" -ForegroundColor Green

# Check if application certificate file exists
if (-not (Test-Path $CertPath)) {
    Write-Host "  ERROR: Application certificate not found at: $CertPath" -ForegroundColor Red
    Write-Host "  Expected file: prisma.pfx" -ForegroundColor Yellow
    exit 1
}
Write-Host "  ✓ Application certificate file found" -ForegroundColor Green

Write-Host ""
Write-Host "[3/5] Installing CA certificate to Trusted Root..." -ForegroundColor Yellow

try {
    # Import CA certificate to Trusted Root (so all signed certs are trusted)
    Import-Certificate -FilePath $CACertPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
    Write-Host "  ✓ CA certificate installed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: Failed to install CA certificate" -ForegroundColor Red
    Write-Host "  $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[4/5] Installing application certificate..." -ForegroundColor Yellow

# Prompt for password if not provided
if ([string]::IsNullOrEmpty($Password)) {
    $securePassword = Read-Host "Enter certificate password" -AsSecureString
} else {
    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
}

try {
    # Import application certificate to Personal store
    Import-PfxCertificate -FilePath $CertPath -CertStoreLocation Cert:\LocalMachine\My -Password $securePassword | Out-Null
    Write-Host "  ✓ Application certificate installed successfully" -ForegroundColor Green
} catch {
    Write-Host "  ERROR: Failed to install application certificate" -ForegroundColor Red
    Write-Host "  Possible causes:" -ForegroundColor Yellow
    Write-Host "    - Incorrect password" -ForegroundColor Yellow
    Write-Host "    - Corrupted PFX file" -ForegroundColor Yellow
    Write-Host "  $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[5/5] Verifying installation..." -ForegroundColor Yellow

# Verify CA certificate
$installedCA = Get-ChildItem Cert:\LocalMachine\Root | Where-Object {$_.Subject -like "*ExxerCube Internal CA*"}
if ($installedCA) {
    Write-Host "  ✓ CA certificate verified in Trusted Root" -ForegroundColor Green
} else {
    Write-Host "  ✗ CA certificate verification FAILED" -ForegroundColor Red
    exit 1
}

# Verify application certificate
$installedAppCert = Get-ChildItem Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*prisma*"}
if ($installedAppCert) {
    Write-Host "  ✓ Application certificate verified in Personal store" -ForegroundColor Green

    # Test certificate validity
    $certValid = Test-Certificate -Cert $installedAppCert -ErrorAction SilentlyContinue
    if ($certValid) {
        Write-Host "  ✓ Certificate chain is VALID" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ WARNING: Certificate chain validation failed" -ForegroundColor Yellow
        Write-Host "    This may cause browser warnings" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ✗ Application certificate verification FAILED" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✓ Certificate installation COMPLETE" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Certificate Details:" -ForegroundColor Cyan
Write-Host "  Subject: $($installedAppCert.Subject)" -ForegroundColor Gray
Write-Host "  Issuer: $($installedAppCert.Issuer)" -ForegroundColor Gray
Write-Host "  Valid From: $($installedAppCert.NotBefore)" -ForegroundColor Gray
Write-Host "  Valid Until: $($installedAppCert.NotAfter)" -ForegroundColor Gray
Write-Host "  Thumbprint: $($installedAppCert.Thumbprint)" -ForegroundColor Gray
Write-Host ""

$daysUntilExpiry = ($installedAppCert.NotAfter - (Get-Date)).Days
Write-Host "Certificate expires in $daysUntilExpiry days" -ForegroundColor $(if ($daysUntilExpiry -lt 30) { "Yellow" } else { "Green" })

if ($daysUntilExpiry -lt 30) {
    Write-Host "⚠ Set calendar reminder to renew certificate!" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Deploy application (see DEPLOYMENT-CHECKLIST.md)" -ForegroundColor Gray
Write-Host "  2. Verify HTTPS works without browser warnings" -ForegroundColor Gray
Write-Host "  3. Document installation date in deployment log" -ForegroundColor Gray
Write-Host ""
