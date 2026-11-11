# CSharp/Python — vendored CSnakes build input

This folder intentionally contains **only `ocr_modules/`**.

## Why it exists

The CSnakes Python interop in `02 Infrastructure/Infrastructure` generates C#
bindings from `Infrastructure/Python/python/prisma_ocr_wrapper.py`. That wrapper
does `from ocr_modules import (...)` (lazily, at runtime). For code generation and
runtime resolution, the build adds this folder to `PYTHONPATH` via the
`SetPythonPathForCSnakes` MSBuild target in
`ExxerCube.Prisma.Infrastructure.csproj`:

```
<_OcrModulesPath>$(_ProjectRoot)\..\..\Python\ocr_modules</_OcrModulesPath>
```

So `ocr_modules/` here is a **vendored copy** of the OCR modules, kept next to the
C# solution purely so the build can resolve them. It is byte-identical to
`Src/Python/Prisma-dumy-generator-AAA/ocr_modules/` (the standalone Python
generator project, which is the upstream source of truth).

## Keep in sync

If `ocr_modules` changes in the AAA generator, update this vendored copy too.

## History

This folder previously also held a full redundant copy (~80 files) of the AAA
generator project (CLIs, extractors, document generator, tests, pyproject, etc.).
Those were removed on 2026-06-07 since the build only references `ocr_modules/`.
See `docs/development/archive/root-cleanup-2026-06.md`.
