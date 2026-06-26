# TASK — Native OCR segfault in the §2 gate on Linux (Tesseract/Leptonica ⟂ SkiaSharp/Emgu coexistence)

**Owner:** dedicated troubleshooting agent · **Branch:** `Liv` · **Authored:** 2026-06-25
**Status:** OPEN — blocks the §2 max-fidelity gate from going green on Linux (Ubuntu 26.04 dev box).
**Type:** native-interop / Linux-portability (part of the active Emgu/Tesseract Linux migration).

---

## 1. Objective

Make `TesseractOcrExecutor` run OCR **without segfaulting** inside the full pipeline process on Linux, so the §2 gate (`MaxFidelityGateFullPipelineE2ETests`) can complete Stage 2 (OCR) and reach export. A green gate additionally needs a consistent corpus — that is a **separate** task (`TASK-GATE-CORPUS-SYNTHETIC-WORKLOAD.md`); do NOT conflate them. This task is done when the gate process survives OCR on a real PDFium-rendered page.

---

## 2. Exact symptom (ground truth, 2026-06-25)

Running the gate:
```
TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata \
dotnet test "Prisma/Code/Src/CSharp/08 Tests/06 E2E/Tests.AllRealWireE2E/ExxerCube.Prisma.Tests.AllRealWireE2E.csproj" \
  --filter-query "/*/*/MaxFidelityGateFullPipelineE2ETests/RealSiaraCase_FlowsAcrossAllThreeProcesses_WithRealPipeline_AndPersistsAudit"
```
crashes the **whole test host** with **exit code 139 (SIGSEGV)** and `Test run summary: Zero tests ran`. The crash happens right after these log lines (the pipeline got this far):
```
[INF] Stage 1 complete: Quality analysis ... Level: Q3_Low          <- Emgu.CV quality analysis OK
[INF] Stage 2: OCR Execution - FileId: ...
[INF] Executing Tesseract OCR on image: .../222AAA-44444444442025.pdf, Page 1/1
[INF] Tessdata path found: /usr/share/tesseract-ocr/5/tessdata       <- tessdata resolves fine
<SIGSEGV — process dies, no further output>
```
So the crash is inside `TesseractOcrExecutor.ProcessWithEngine` → either `Pix.LoadFromMemory(...)` or `engine.Process(pix)` (`Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Extraction/Teseract/TesseractOcrExecutor.cs`, ~lines 176-204).

---

## 3. What is already PROVEN (do not re-investigate)

- **Native Tesseract is fine in isolation on this box.** `dotnet test "Prisma/Code/Src/CSharp/08 Tests/02 Infrastructure/Tests.Infrastructure.Extraction.Teseract/ExxerCube.Prisma.Tests.Infrastructure.Extraction.Teseract.csproj"` (with `TESSDATA_PREFIX` set) **PASSES, 0 failed, ~20s**. That suite loads `libtesseract50.so` + `libleptonica-1.82.0.so` (Tesseract 5.2.0 NuGet, in the build output `x64/`) and does real OCR. It does NOT load SkiaSharp or Emgu.CV.
- **The gate process additionally loads two other native imaging stacks** before OCR:
  - **SkiaSharp / PDFtoImage** (`PdfToImageConverter`, `Prisma/Code/Src/CSharp/02 Infrastructure/Infrastructure.Extraction/Teseract/PdfToImageConverter.cs`) — rasterizes the PDF page (PDFium). `libSkiaSharp.so` **statically bundles its own libpng/libjpeg**.
  - **Emgu.CV / OpenCV** (`PolynomialImageQualityAnalyzer`, Stage 1) — `libcvextern.so`. It runs successfully (Stage 1 completes) BEFORE the crash.
- `ldd` shows both `libcvextern.so` and `libleptonica-1.82.0.so` bind the **system** codec libs (`libpng16.so.16`, `libjpeg.so.8`, `libtiff.so.6`, `libwebp.so.7`); SkiaSharp's are statically-linked internal copies.
- The Linux OCR *plumbing* is already fixed + committed (`5f95b7e6`): `FileSystemLoader` no longer Windows-gates image load (PDFium path works → "Rasterized PDF: 4 page(s) rendered"), and `TesseractOcrExecutor.GetTessdataPathAsync` now includes `/usr/share/tesseract-ocr/5/tessdata`. **Do not redo these.**

**Conclusion already reached:** this is a **multi-native-imaging-library coexistence crash** — Tesseract/Leptonica segfaults once SkiaSharp (and/or Emgu) are co-resident in the process, almost certainly libpng/codec **symbol interposition** (two libpng in one process; whichever was loaded with global scope wins, and the struct layouts differ).

---

## 4. Bounded attempt already tried (FAILED — don't repeat verbatim)

Re-encoding the OCR input to **BMP** instead of PNG before `Pix.LoadFromMemory` (so Leptonica uses its self-contained BMP reader, no libpng). **Still segfaulted** → the crash is **past image decode** (in OCR processing / leptonica image ops, or a deeper symbol clash), not in PNG decode. The change was reverted (uncommitted).

---

## 5. Recommended investigation path (in order)

### 5a. Localize: coexistence vs. the specific image
Write a tiny standalone repro (xUnit fact in a scratch test project, or a console app) that, **in ONE process**:
1. calls `PdfToImageConverter.ConvertToImagesAsync` on `Prisma/Fixtures/PRP1/222AAA-44444444442025.pdf` (loads SkiaSharp/PDFium), then
2. feeds the resulting bytes to `TesseractOcrExecutor.ExecuteOcrAsync`.
- If it **segfaults** → coexistence confirmed (SkiaSharp is sufficient to trigger; Emgu not required). Proceed to 5b/5c.
- If it **passes** → add the Emgu Stage-1 (`PolynomialImageQualityAnalyzer`) call before OCR and retry; if THAT segfaults, Emgu is the trigger.
- Also try: feed the **exact rendered page** (dump it to a file in the gate run, then) to the *standalone* Tesseract suite path (no Skia/Emgu) — if that passes, it's coexistence, not the image.

### 5b. Confirm the symbol-interposition mechanism
- Run the repro with `LD_DEBUG=bindings LD_DEBUG_OUTPUT=/tmp/lddbg dotnet test ...` and grep the output for `png_`/`jpeg_`/`Tess`/`lept` symbol bindings — look for Leptonica's libpng calls resolving to SkiaSharp's statically-exported libpng symbols.
- `cat /proc/<pid>/maps` (or `gdb` with `catch signal SIGSEGV` + `bt`) at crash to get the native stack. A core dump + `bt` is the single most decisive artifact — get one if at all possible (`ulimit -c unlimited`, `coredumpctl`).

### 5c. Candidate fixes (pick by what 5a/5b reveal)
- **Force a single libpng/leptonica into the process** with controlled load order: `LD_PRELOAD=/usr/lib/x86_64-linux-gnu/libpng16.so.16:/usr/lib/x86_64-linux-gnu/libjpeg.so.8` on the gate/worker so the SYSTEM libpng is authoritative and SkiaSharp's internal copy doesn't interpose. (Test this first — it may be a near-zero-code fix, configurable via the worker launch / Dockerfile env.)
- **Avoid SkiaSharp for rasterization**: swap `PdfToImageConverter` to render via a stack that shares Leptonica's codecs (e.g. PDFium through a different binding, or render via Emgu/ImageSharp-only) so only one libpng is resident. Larger change.
- **Process isolation**: run OCR in a short-lived child process / out-of-proc worker so Tesseract never shares an address space with SkiaSharp. Most robust, biggest change; aligns with future scaling.
- **Rebuild/align native versions**: make the Tesseract NuGet's bundled `libleptonica-1.82.0.so` use the system codec libs consistently, or pin SkiaSharp to a build that dynamically links system libpng.

Prefer the **least-invasive fix that survives in production** (the workers run in containers — see the Dockerfiles under each host). `LD_PRELOAD` via the container env is attractive if it works.

---

## 6. Definition of done

- The §2 gate (command in §2) reaches **past Stage 2 OCR without SIGSEGV** — OCR returns text (Stage 2 logs `extraction complete` / non-empty chars), and the run proceeds to fusion/classification/export (it may still BLOCK at the export gate on the corpus issue — that's the OTHER task; this task only owns "no segfault, OCR runs").
- The standalone Tesseract suite still passes (no regression).
- The fix is committed to `Liv` with a clear rationale + the chosen mechanism, and (if it's `LD_PRELOAD`/env) wired into the worker launch / Dockerfiles so it holds in deployment, not just the test.
- Build 0/0; `Tests.Infrastructure.Extraction.Teseract` green.

---

## 7. Gotchas / box state (reusable)

- Box: Ubuntu 26.04, Docker 29.6, dotnet 10.0.301, native Tesseract 5 (`/usr/share/tesseract-ocr/5/tessdata`, has spa/eng/osd).
- Gate needs: Docker (Testcontainers SQL 2025), Playwright Chromium (symlink `~/.cache/ms-playwright/chromium-1223 → chromium-1228` because PW 1.60 refuses the ubuntu26.04 download), `sudo sysctl kernel.apparmor_restrict_unprivileged_userns=0` (chromium sandbox), and the SIARA sim on `http://localhost:5001`. Full recipe: `EXECUTION-TRACKER.md` 2026-06-25 handoff.
- A core dump + native backtrace will save hours — get one before theorizing.
- This is the owner's active Emgu/Tesseract Linux migration; coordinate before large native changes.
