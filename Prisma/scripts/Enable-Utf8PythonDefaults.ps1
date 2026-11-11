#requires -Version 7.0
<#
.SYNOPSIS
Ensures PowerShell and Python use UTF-8 defaults so emoji output works without per-session tweaks.

.DESCRIPTION
Appends an idempotent configuration block to user PowerShell profiles so every session:
* Forces code page 65001
* Sets the console output encoding to UTF-8
* Sets PYTHONUTF8=1 and PYTHONIOENCODING=utf-8

Also persists the Python environment variables at the user scope so Python launched outside
PowerShell inherits UTF-8 behaviour.  The script is safe to re-run; it skips duplicate inserts.

.PARAMETER DryRun
Reports the actions without changing anything.

.NOTES
Run this once per user account.  You still need to enable Windows' “Beta: Use Unicode UTF-8”
system locale option manually because it requires a reboot and administrative consent.
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$EnableSystemUtf8
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$profileBlock = @'
# >>> UTF-8 + Python emoji defaults (Enable-Utf8PythonDefaults.ps1) >>>
try { chcp 65001 | Out-Null } catch { Write-Verbose "chcp failed: $($_.Exception.Message)" }
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { Write-Verbose "OutputEncoding failed: $($_.Exception.Message)" }
Set-Item env:PYTHONUTF8 1
Set-Item env:PYTHONIOENCODING 'utf-8'
# <<< UTF-8 + Python emoji defaults (Enable-Utf8PythonDefaults.ps1) <<<
'@

function Add-ProfileBlock {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $marker = '# >>> UTF-8 + Python emoji defaults (Enable-Utf8PythonDefaults.ps1) >>>'
    $directory = Split-Path -Parent $Path

    if (-not (Test-Path $directory)) {
        if ($DryRun) {
            Write-Verbose "Would create $Description profile directory '$directory'"
        }
        else {
            Write-Verbose "Creating $Description profile directory '$directory'"
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
        }
    }

    $existing = ''
    if (Test-Path $Path) {
        $existing = Get-Content -Path $Path -Raw
        if ($existing -match [Regex]::Escape($marker)) {
            Write-Verbose "$Description profile already contains UTF-8 block"
            return
        }
    }

    if ($DryRun) {
        Write-Verbose "Would append UTF-8 block to $Description profile '$Path'"
        return
    }

    Write-Verbose "Appending UTF-8 block to $Description profile '$Path'"
    if ([string]::IsNullOrEmpty($existing)) {
        $profileBlock | Set-Content -Path $Path -Encoding utf8
    }
    else {
        if (-not $existing.EndsWith("`n")) {
            "`n" | Add-Content -Path $Path -Encoding utf8
        }

        $profileBlock | Add-Content -Path $Path -Encoding utf8
    }
}

$registryPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\Nls\CodePage'
$registryTargets = @{
    ACP   = '65001'
    OEMCP = '65001'
}

function Test-IsAdministrator {
    $current = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($current)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Enable-SystemUtf8Locale {
    if (-not $EnableSystemUtf8) {
        return
    }

    if (-not (Test-IsAdministrator)) {
        throw 'EnableSystemUtf8 requires an elevated PowerShell session (Run as administrator).'
    }

    foreach ($entry in $registryTargets.GetEnumerator()) {
        $currentValue = (Get-ItemProperty -Path $registryPath -Name $entry.Key -ErrorAction SilentlyContinue).$($entry.Key)
        if ($DryRun) {
            if ($currentValue -eq $entry.Value) {
                Write-Verbose ("Would keep {0}={1}" -f $entry.Key, $entry.Value)
            }
            else {
                Write-Verbose ("Would set {0} from {1} to {2}" -f $entry.Key, $currentValue, $entry.Value)
            }
        }
        else {
            if ($currentValue -ne $entry.Value) {
                Write-Verbose ("Setting {0}={1}" -f $entry.Key, $entry.Value)
                Set-ItemProperty -Path $registryPath -Name $entry.Key -Value $entry.Value -Force
            }
            else {
                Write-Verbose ("{0} already set to {1}" -f $entry.Key, $entry.Value)
            }
        }
    }
}

$profileTargets = @(
    @{ Path = $PROFILE.CurrentUserAllHosts; Description = 'PowerShell (all hosts)' },
    @{ Path = $PROFILE; Description = 'PowerShell (current host)' },
    @{
        Path = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WindowsPowerShell\Microsoft.PowerShell_profile.ps1'
        Description = 'Windows PowerShell'
    }
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Path) } | Sort-Object Path -Unique

foreach ($target in $profileTargets) {
    Add-ProfileBlock -Path $target.Path -Description $target.Description
}

$envVariables = @{
    PYTHONUTF8        = '1'
    PYTHONIOENCODING  = 'utf-8'
}

foreach ($kvp in $envVariables.GetEnumerator()) {
    if ($DryRun) {
        Write-Verbose "Would persist $($kvp.Key)=$($kvp.Value)"
    }
    else {
        Write-Verbose "Persisting $($kvp.Key)=$($kvp.Value)"
        [Environment]::SetEnvironmentVariable($kvp.Key, $kvp.Value, [EnvironmentVariableTarget]::User)
        Set-Item -Path ("env:{0}" -f $kvp.Key) -Value $kvp.Value
    }
}

if (-not $DryRun) {
    try {
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    }
    catch {
        Write-Warning "Unable to set current console output encoding: $($_.Exception.Message)"
    }

    try {
        chcp 65001 | Out-Null
    }
    catch {
        Write-Warning "Unable to set current console code page: $($_.Exception.Message)"
    }
}

Enable-SystemUtf8Locale

Write-Host "✅ UTF-8 defaults applied for PowerShell sessions." -ForegroundColor Green
Write-Host "   Persistent Python environment variables set for the current user." -ForegroundColor Green
if ($EnableSystemUtf8) {
    Write-Host "⚠️  System UTF-8 locale updated. Restart Windows to finish applying the change." -ForegroundColor Yellow
}
else {
    Write-Host "ℹ️  Run this script with -EnableSystemUtf8 from an elevated PowerShell to toggle the system-wide UTF-8 locale automatically." -ForegroundColor Yellow
}
