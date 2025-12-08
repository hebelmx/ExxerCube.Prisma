# Quick Test Run Guide

## Option 1: Full Setup + Test (First Time)

```bash
# 1. Setup venv manually (10 min)
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp\Tests.Infrastructure.Extraction.GotOcr2
setup_manual_venv.bat

# 2. Run tests (15 min first time - downloads model)
cd ..
dotnet test Tests.Infrastructure.Extraction.GotOcr2 --verbosity normal
```

## Option 2: Quick Test (If venv exists)

```bash
cd F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\Code\Src\CSharp
dotnet test Tests.Infrastructure.Extraction.GotOcr2 --verbosity normal
```

## Option 3: Single Test (Debug)

```bash
dotnet test Tests.Infrastructure.Extraction.GotOcr2 \
  --filter "FullyQualifiedName~should reject empty image data" \
  --verbosity detailed
```

## Expected Success Output:

```
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
Duration: ~2 minutes
```

## If Tests Fail:

1. **Check Python logging fix:**
   ```bash
   grep "import logging" bin/.../net10.0/python/got_ocr2_wrapper.py
   ```

2. **Check venv exists:**
   ```bash
   dir bin\ExxerCube.Prisma.Tests.Infrastructure.Extraction.GotOcr2\net10.0\.venv_gotor2_tests
   ```

3. **Re-run setup:**
   ```bash
   setup_manual_venv.bat
   ```

See `FIXES_APPLIED.md` for complete troubleshooting guide.
