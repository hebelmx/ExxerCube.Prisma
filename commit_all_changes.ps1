# PowerShell script to commit all changes
# Run this script from the repository root

Write-Host "Staging all changes..." -ForegroundColor Green
git add -A

Write-Host "Checking status..." -ForegroundColor Green
git status --short

Write-Host "`nCommitting changes..." -ForegroundColor Green
git commit -m "feat: Add VEC Statement Processing Python components and expanded architecture

- Add Python visual and font identification modules (CLIP, PyTorch, Pydantic)
- Add user stories for Python ML pipeline (PYTHON-001 through PYTHON-006)
- Expand architecture document with VEC Statement Processing details
- Add CSnakes integration wrapper for C# interop
- Add comprehensive Pydantic models for logo, font, and quality detection
- Add pytest test infrastructure
- Update architecture for ExxerCube.Prisma.Veriqan namespace compatibility
- Add all modified and new files from development session"

Write-Host "`nCommit completed!" -ForegroundColor Green
Write-Host "Recent commits:" -ForegroundColor Cyan
git log --oneline -3
