# Docker Full Reset Script for Windows
# CAUTION: This will remove Docker Desktop, clear WSL Docker-related data, and reset Docker settings

Write-Host "Stopping Docker Desktop..." -ForegroundColor Cyan
Stop-Process -Name "Docker Desktop" -Force -ErrorAction SilentlyContinue

Write-Host "Killing related WSL processes..." -ForegroundColor Cyan
wsl --shutdown

Write-Host "Unregistering docker-desktop and docker-desktop-data WSL distros..." -ForegroundColor Yellow
wsl --unregister docker-desktop
wsl --unregister docker-desktop-data

Start-Sleep -Seconds 3

Write-Host "Removing Docker Desktop app (if installed via winget)..." -ForegroundColor Cyan
# Note: Requires Windows Package Manager (winget)
winget uninstall -e --id Docker.DockerDesktop

Write-Host "Removing Docker config folders..." -ForegroundColor Cyan
$dockerAppData = "$env:APPDATA\Docker"
$dockerLocalAppData = "$env:LOCALAPPDATA\Docker"
$dockerCliConfig = "$env:USERPROFILE\.docker"
$programDataDocker = "C:\ProgramData\Docker"

Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $dockerAppData
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $dockerLocalAppData
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $dockerCliConfig
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $programDataDocker

Write-Host "Clearing Docker networks and NAT config (requires Admin privileges)..." -ForegroundColor Yellow
Get-HNSNetwork | Remove-HNSNetwork -ErrorAction SilentlyContinue

Write-Host "Reset complete. Please reboot your system before reinstalling Docker Desktop." -ForegroundColor Green
