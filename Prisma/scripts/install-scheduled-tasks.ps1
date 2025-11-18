# Install Scheduled Tasks for Type Database Updates
# Runs at 10 AM and 10 PM daily
# Run this script as Administrator: Right-click > Run as Administrator

$ProjectPath = "F:\Dynamic\ExxerAi\ExxerAI"
$ScriptPath = "$ProjectPath\scripts\update_type_database.py"
$PythonExe = (Get-Command python).Path

Write-Host "Installing ExxerAI Type Database Update Scheduled Tasks..." -ForegroundColor Cyan
Write-Host "Project Path: $ProjectPath" -ForegroundColor Gray
Write-Host "Script Path: $ScriptPath" -ForegroundColor Gray
Write-Host "Python Path: $PythonExe" -ForegroundColor Gray
Write-Host ""

# Task 1: 10 AM Daily Update
$TaskName1 = "ExxerAI Type Database Update - 10 AM"
$Action1 = New-ScheduledTaskAction -Execute $PythonExe `
    -Argument "$ScriptPath" `
    -WorkingDirectory $ProjectPath

$Trigger1 = New-ScheduledTaskTrigger -Daily -At "10:00AM"

$Settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RunOnlyIfNetworkAvailable:$false

$Principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType S4U

Register-ScheduledTask -TaskName $TaskName1 `
    -Action $Action1 `
    -Trigger $Trigger1 `
    -Settings $Settings `
    -Principal $Principal `
    -Description "Updates ExxerAI type database at 10 AM daily for intelligent type search" `
    -Force | Out-Null

Write-Host "✅ Installed: $TaskName1" -ForegroundColor Green

# Task 2: 10 PM Daily Update
$TaskName2 = "ExxerAI Type Database Update - 10 PM"
$Action2 = New-ScheduledTaskAction -Execute $PythonExe `
    -Argument "$ScriptPath" `
    -WorkingDirectory $ProjectPath

$Trigger2 = New-ScheduledTaskTrigger -Daily -At "10:00PM"

Register-ScheduledTask -TaskName $TaskName2 `
    -Action $Action2 `
    -Trigger $Trigger2 `
    -Settings $Settings `
    -Principal $Principal `
    -Description "Updates ExxerAI type database at 10 PM daily for intelligent type search" `
    -Force | Out-Null

Write-Host "✅ Installed: $TaskName2" -ForegroundColor Green
Write-Host ""

# Verify installation
Write-Host "Verifying installation..." -ForegroundColor Cyan
Get-ScheduledTask -TaskName "*ExxerAI Type Database*" | Format-Table -Property TaskName, State, @{Label="Next Run Time"; Expression={$_.NextRunTime}}

Write-Host ""
Write-Host "✅ Setup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "💡 To manage tasks:" -ForegroundColor Yellow
Write-Host "   - Open Task Scheduler: taskschd.msc" -ForegroundColor Gray
Write-Host "   - Look for: 'ExxerAI Type Database Update'" -ForegroundColor Gray
Write-Host ""
Write-Host "🔧 To test manually:" -ForegroundColor Yellow
Write-Host "   python `"$ScriptPath`"" -ForegroundColor Gray
Write-Host ""
Write-Host "🗑️  To uninstall:" -ForegroundColor Yellow
Write-Host "   Unregister-ScheduledTask -TaskName 'ExxerAI Type Database Update - 10 AM' -Confirm:`$false" -ForegroundColor Gray
Write-Host "   Unregister-ScheduledTask -TaskName 'ExxerAI Type Database Update - 10 PM' -Confirm:`$false" -ForegroundColor Gray
