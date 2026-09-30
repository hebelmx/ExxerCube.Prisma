# Git Detective - Find out what VS did to your code
Write-Host "🔍 Git Timeline Detective" -ForegroundColor Cyan

# Show recent commits with full details
Write-Host "`n📅 Recent commits:" -ForegroundColor Yellow
git log --oneline -20

# Show files changed in last few commits
Write-Host "`n📝 Files touched recently:" -ForegroundColor Yellow  
git log --name-status -10 | Select-String -Pattern "^[MAD]\s"

# Check for any weird merges or reverts
Write-Host "`n🔄 Checking for reverts/merges:" -ForegroundColor Yellow
git log --grep="revert\|merge\|Merge" --oneline -20

# Show what changed in specific files
Write-Host "`n🎯 Enter filename to investigate (or Enter to skip): " -NoNewline
$file = Read-Host
if ($file) {
    Write-Host "`nHistory of $file:" -ForegroundColor Cyan
    git log -p -5 -- $file
}

# Check reflog for unusual activity
Write-Host "`n👻 Git reflog (local history):" -ForegroundColor Yellow
git reflog -10

# Show current status
Write-Host "`n📊 Current status:" -ForegroundColor Yellow
git status

Write-Host "`n💡 Tip: If you see commits you didn't make, VS probably auto-committed!" -ForegroundColor Green