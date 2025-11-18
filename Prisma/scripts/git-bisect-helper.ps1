# Git Bisect Helper - Find when Visual Studio broke your code
# Especially useful when you have commits every 5 minutes

Write-Host "🔍 Git Bisect Helper - Finding when code broke" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# First, let's see the commit range
Write-Host "`n📊 Recent commit summary:" -ForegroundColor Yellow
$commits = git log --oneline -50 --pretty=format:"%h %ad %s" --date=format:"%m/%d %H:%M"
$commits | ForEach-Object { Write-Host $_ }

Write-Host "`n" -NoNewline
Write-Host "Enter GOOD commit hash (when code worked): " -NoNewline -ForegroundColor Green
$goodCommit = Read-Host

Write-Host "Enter BAD commit hash (current broken state, or press Enter for HEAD): " -NoNewline -ForegroundColor Red
$badCommit = Read-Host
if ([string]::IsNullOrEmpty($badCommit)) { $badCommit = "HEAD" }

# Start bisect
Write-Host "`n🎯 Starting git bisect..." -ForegroundColor Yellow
git bisect start
git bisect bad $badCommit
git bisect good $goodCommit

Write-Host "`n📝 Git will now checkout commits. For each one:" -ForegroundColor Cyan
Write-Host "1. Run: dotnet build --no-restore" -ForegroundColor White
Write-Host "2. If builds: type 'git bisect good'" -ForegroundColor Green
Write-Host "3. If fails: type 'git bisect bad'" -ForegroundColor Red
Write-Host "4. Repeat until git finds the bad commit" -ForegroundColor White

Write-Host "`n🤖 Or use automated bisect:" -ForegroundColor Yellow
Write-Host "git bisect run powershell -Command `"dotnet build --no-restore; exit `$LASTEXITCODE`"" -ForegroundColor Gray

Write-Host "`n❌ To abort bisect: git bisect reset" -ForegroundColor Red