# TRACKER — O1: Athena container OCR (epic, orchestrated)

**Origin:** `OPEN-BACKLOG-2026-07-25.md` §1 O1 — the only Blocks-severity buildable item.
**Branch:** `Liv` (from HEAD `638335a1`). **Status:** ✅ DONE (2026-07-27; verified in-container).

## Problem
`Prisma.Athena.Worker/Dockerfile` runtime stage is bare `aspnet:10.0` — no tesseract/leptonica,
no Emgu/OpenCV natives, no link shims, no tessdata. Athena IS the production OCR host
(`TesseractOcrExecutor` @ Program.cs:161; Linux interop guard @ :32-34 proves Emgu `libcvextern.so`
is co-resident in Stage 1). Only the Web.UI image got the OCR stack (GH#28), and only Tesseract.

## Intended solution (orchestrator decision record)
1. **Emgu native for noble:** runtime base image `aspnet:10.0` = Ubuntu 24.04 (noble), but the repo
   pins `Emgu.CV.runtime.ubuntu-26.04-x64` (dev box is 26.04; its libcvextern needs VTK 9.5/HDF5 310
   absent on noble). **Verified on NuGet:** `Emgu.CV.runtime.ubuntu-24.04-x64` exists at exactly
   `4.13.0.5924`. → Introduce MSBuild property `EmguLinuxRuntimePackage` in
   `Infrastructure.Imaging.csproj` (default `Emgu.CV.runtime.ubuntu-26.04-x64`; Athena Dockerfile
   passes `-p:EmguLinuxRuntimePackage=Emgu.CV.runtime.ubuntu-24.04-x64`). Add CPM `PackageVersion`
   pin for the 24.04 package (same version). Host/dev behavior unchanged.
2. **Runtime stage OCR stanza (mirror Web.UI GH#28, plus Emgu):** apt `tesseract-ocr
   tesseract-ocr-spa tesseract-ocr-eng`; run BOTH `eng/link-tesseract-natives.sh /app` and
   `eng/link-emgu-native.sh /app/` (note trailing slash — emgu script concatenates without one);
   apt the libcvextern system deps for the 24.04 build (discover empirically via `ldd
   /app/runtimes/*/native/libcvextern.so` in-container; expected family: libvtk9.1, hdf5, libavif16,
   libgeotiff5, liblapack3 — noble sonames).
3. **Proof — `--ocr-smoke` mode in Athena Worker:** early-exit path in Program.cs BEFORE host/DB
   wiring (but AFTER the native interop guard): use Emgu (`CvInvoke.PutText` or similar) to render
   known text onto a bitmap, save PNG to temp, OCR it via `TesseractOcrExecutor`, assert recognized
   text contains the marker. Exit 0 on success / 1 on failure with clear console output. Exercises
   BOTH native stacks + the co-residency guard, with zero DB/hub/config dependencies.
4. **Compose:** `docker-compose.dev.yml` athena service builds this same Dockerfile — no compose
   change expected; verify only.

## Chunks
| # | Chunk | Status | Evidence |
|---|-------|--------|----------|
| C1 | Emgu package switch + Dockerfile OCR stack + `--ocr-smoke` | ✅ DONE | Diff on the 4 planned files + new `OcrContainerSmokeTest.cs`; agent's apt list was missing FFmpeg/GStreamer (its docker proof never completed — session died mid-build); orchestrator closed the gap via in-image `ldd` iteration (`libavcodec60 libavformat60 libavutil58 libswscale7 libgstreamer1.0-0 libgstreamer-plugins-base1.0-0`). |
| C2 | Orchestrator ground-truth verify | ✅ DONE | Host build 0 W / 0 E; `Prisma.Athena.Worker.Tests` 26/26 ×2; `docker run … --ocr-smoke` in `prisma-athena:o1-smoke` → EXIT 0, recognized `"PRISMA OCR SMOKE 12345"` (spa model, conf ≈92%), tessdata found at `/usr/share/tesseract-ocr/5/tessdata`. |
| C3 | Adversarial review + fixes, commits+push, backlog/memory update | ✅ DONE | Review: no BLOCKER/MAJOR; 3 fixes applied — apt layer moved BEFORE publish COPY (cache/registry cost), smoke switched `eng`→`spa` (guards the language production's once-per-process engine init actually uses), CI `--ocr-smoke` step added to `quality-gates.yml` docker-build job. Re-verified green after fixes. |

## Adversarial-review residuals (recorded, not in O1 scope)
- **Web.UI container has the same Emgu hole** (pre-existing): references Infrastructure.Imaging /
  `EmguCvImageQualityAnalyzer`, but its Dockerfile has only the Tesseract stanza — the default 26.04
  `libcvextern.so` in its publish output cannot load on its noble runtime stage. O1's fix pattern
  (package override + emgu shim + apt deps) applies verbatim. → backlog follow-up.
- **Trivy CRITICAL gate on publish** now scans a much larger CVE surface (ffmpeg/gstreamer/gtk/vtk/hdf5
  in the Athena image); a fixed-CRITICAL in any of them blocks main-branch publish. Operational note.
- Image size grew 1.53 GB → ~2.7 GB (compressed 410→~700 MB) — the price of the native OCR stack.

## Gotchas for anyone resuming
- `LinkEmguLinuxNative`/`LinkTesseractLinuxNatives` MSBuild targets run against `$(TargetDir)` on
  Build/Publish — symlinks do NOT usefully survive into the runtime image; shims must re-run in the
  runtime stage (Web.UI precedent, Dockerfile lines 62-67).
- `.dockerignore` excludes `**/Fixtures/` except `Prisma/Fixtures/PRP1/**` — the smoke deliberately
  needs NO fixtures (renders its own test image).
- `UseRidGraph=true` (Linux, Directory.Build.props) is required for distro-RID resolution — already set.
- tessdata lands at `/usr/share/tesseract-ocr/5/tessdata` via apt; `TesseractOcrExecutor` probes it.
