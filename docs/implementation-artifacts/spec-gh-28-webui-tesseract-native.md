---
title: 'GH#28 — Web.UI PDF OCR "failed for all pages": Tesseract native stack missing from the image'
type: 'bugfix'
created: '2026-07-02'
status: 'done'
baseline_commit: 'cc547c98'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `/document-processing` PDF OCR fails "OCR failed for all pages". The Web.UI image has
no Tesseract native stack — only the Windows DLLs the `Tesseract` NuGet ships
(`/app/x64/tesseract50.dll`, `leptonica-1.82.0.dll`) and no `.traineddata`. The charlesw
`Tesseract 5.2.0` wrapper P/Invokes the Linux sonames `libtesseract50.so` / `libleptonica-1.82.0.so`
(+ `libdl.so`), which apt does not provide (noble ships `libtesseract.so.5` / `liblept.so.5`).

**Approach:** In the runtime stage: (1) apt-install `tesseract-ocr` + `tesseract-ocr-spa` +
`tesseract-ocr-eng` (native libs + tessdata at `/usr/share/tesseract-ocr/5/tessdata`, which
`TesseractOcrExecutor.GetTessdataPathAsync` already probes); (2) run the repo's existing
`eng/link-tesseract-natives.sh` shim to symlink the ABI-compatible system libs to the
wrapper-expected names under `/app/x64`. Same mechanism as the Emgu native shim and the 154
green `Extraction.Teseract` tests.

## Boundaries & Constraints

**Always:** Install + link in the **runtime** stage (the SDK build stage has no Tesseract, so the
MSBuild `LinkTesseractLinuxNatives` target no-ops there). Reuse `link-tesseract-natives.sh`. Keep
`spa` + `eng` language data (Spanish legal docs; eng for mixed content).

**Never:** Do NOT change the `Tesseract` NuGet version. Do NOT hardcode a tessdata path in
appsettings (the executor probes). Do NOT delete the Windows DLLs (the link script keys off
`x64/leptonica-1.82.0.dll` to detect a Tesseract-consuming output).

## I/O & Edge-Case Matrix

| Scenario | State | Expected | Error Handling |
|----------|-------|----------|----------------|
| PDF OCR after fix | natives linked, spa/eng data | text extracted, no "OCR failed" | N/A |
| leptonica soname `liblept.so.5` | noble base | link script now matches `liblept` (broadened) → symlink created | fixed regex |
| tessdata missing lang | only osd present | executor logs + fails that page | pre-existing |

</frozen-after-approval>

## Code Map

- `.../Web.UI/Dockerfile` — runtime stage: apt-install tesseract + spa/eng; COPY + run link shim against `/app`.
- `eng/link-tesseract-natives.sh:21` — broadened leptonica match `libleptonica\.so` → `liblept` (matches `liblept.so.5` on noble AND `libleptonica.so.6`). The old regex silently skipped `liblept.so.5`.
- `.../Teseract/TesseractOcrExecutor.cs:375` — already probes `/usr/share/tesseract-ocr/5/tessdata`; unchanged.

## Tasks & Acceptance

- [x] Dockerfile — apt-install `tesseract-ocr tesseract-ocr-spa tesseract-ocr-eng`; COPY + run `link-tesseract-natives.sh /app`.
- [x] `link-tesseract-natives.sh` — broaden leptonica soname match to `liblept`.

**Acceptance / Verified 2026-07-02:**
- Image: `tesseract 5.3.4`; symlinks `x64/libtesseract50.so → libtesseract.so.5`,
  `x64/libleptonica-1.82.0.so → liblept.so.5`, `libdl.so → libdl.so.2`; `spa`+`eng`+`osd` tessdata present.
- `ldd` on both symlinked `.so`s → **ALL RESOLVED** (no missing deps → wrapper `dlopen` succeeds).
- Live engine OCR in-container: rendered "Oficio SIARA 2025" → `tesseract -l spa` returned it exactly.
- App boots, `GET /` → 200. Final pixel-level confirmation = the demo's Process-PDF click.
