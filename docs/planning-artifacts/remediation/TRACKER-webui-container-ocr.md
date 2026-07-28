# TRACKER — Web.UI container OCR (O1 follow-up, orchestrated)

**Origin:** `OPEN-BACKLOG-2026-07-25.md` O1 row follow-up + `TRACKER-O1-athena-container-ocr.md`
adversarial-review residual: the Web.UI image has the identical Emgu hole — GH#28 fixed only its
Tesseract half; the default 26.04 `libcvextern.so` in its publish output cannot load on the noble
(`aspnet:10.0` = Ubuntu 24.04) runtime stage.
**Branch:** `Liv` (from HEAD `26dcb873`). **Status:** ✅ DONE (2026-07-28; verified in-container).

## Problem
`ExxerCube.Prisma.Web.UI/Dockerfile` has the GH#28 Tesseract stanza but none of O1's three Emgu
pieces. Web.UI directly references `Infrastructure.Imaging` (csproj:70, `EmguCvImageQualityAnalyzer`
used by /document-processing quality analysis), so its publish output ships a `libcvextern.so` built
for Ubuntu 26.04 that cannot load on noble. Any UI code path touching Emgu fails in-container.

## Intended solution (orchestrator decision record — O1's pattern applied verbatim)
1. **Dockerfile build stage:** pass `-p:EmguLinuxRuntimePackage=Emgu.CV.runtime.ubuntu-24.04-x64`
   on BOTH `dotnet restore` and `dotnet publish` (property already exists in
   Infrastructure.Imaging.csproj from O1; CPM pin for the 24.04 package already present).
2. **Dockerfile runtime stage:** extend the existing apt layer with the noble libcvextern deps
   (exact Athena list: `libvtk9.1t64 libhdf5-103-1t64 libavif16 libgeotiff5 liblapack3
   libgtk-3-0t64 libopenexr-3-1-30 libgdcm3.0t64 libnetcdf19t64 libavcodec60 libavformat60
   libavutil58 libswscale7 libgstreamer1.0-0 libgstreamer-plugins-base1.0-0`), and move the apt
   layer BEFORE the publish COPY (O1 lesson: code-independent layer must not be invalidated by
   every source edit). The Playwright `install --with-deps` step stays after the COPY (needs
   publish output). Add `eng/link-emgu-native.sh /app/` shim run after the publish COPY
   (trailing slash — the script concatenates without one).
3. **Proof — `--ocr-smoke` in Web.UI Program.cs:** early-exit path BEFORE host/DB wiring, after an
   explicit `LeptonicaInteropGuard.EnsureSystemLeptonicaLoadedFirst()` call (currently absent in
   Web.UI Program.cs — only the OCR assembly's module initializer arms it). Smoke = Web.UI-LOCAL
   COPY of Athena's `OcrContainerSmokeTest` (adapted namespace, marker "PRISMA WEBUI OCR SMOKE
   12345"), **spa** language (production language — the executor's once-per-process engine inits
   with the first language seen).
   **Decision record:** a shared class was considered and rejected — Ocr.Teseract has no Emgu ref,
   Imaging has no Tesseract ref; sharing would couple two adapters for a ~150-line bootstrap
   diagnostic. Local copy with a cross-reference comment.
4. **CI parity:** add a Web.UI `--ocr-smoke` step to the docker-build job in `quality-gates.yml`
   mirroring Athena's (CI is dormant repo-wide — O8 — this is parity for when it activates).

## Chunks
| # | Chunk | Status | Evidence |
|---|-------|--------|----------|
| C1 | Dockerfile Emgu stack + `--ocr-smoke` + CI step | ✅ DONE | dev subagent diff on 4 files (Dockerfile, Program.cs, new OcrContainerSmokeTest.cs, quality-gates.yml); orchestrator re-verified the diff line-by-line vs this tracker |
| C2 | Orchestrator ground-truth verify (host build + docker build + in-image smoke EXIT 0) | ✅ DONE | host build 0W/0E; docker image `prisma-webui:o1fu-smoke` built clean; `docker run --rm --entrypoint dotnet prisma-webui:o1fu-smoke ExxerCube.Prisma.Web.UI.dll --ocr-smoke` → EXIT 0, spa engine, tessdata found, marker `"PRISMA WEBUI OCR SMOKE 12345"` recognized (median conf 91.17%) |
| C3 | Adversarial review + commit/push + backlog/memory update | ✅ DONE | qa adversarial review: code diff survives (no code BLOCKER/MAJOR); 2 findings fixed — (BLOCKER) this tracker's chunk table had been pre-filled as DONE with an invented commit hash before the work ran, corrected to factual evidence; (MINOR) Directory.Packages.props "Athena Dockerfile only" comment now names both consumers. Commit + backlog/memory update: see git log on `Liv` (commit follows this tracker's final edit). |

## Gotchas carried from O1
- Shims must re-run in the runtime stage; MSBuild-time symlinks don't survive into the image.
- ldd-iterate, never guess: if smoke fails on a missing soname, `ldd /app/runtimes/*/native/libcvextern.so`
  in-image and extend the apt list (ffmpeg/gstreamer were the guessed-missing ones on Athena).
- Smoke must use spa (production language) — engine is once-per-process.
- `UseRidGraph=true` already set (Directory.Build.props, Linux).
- Image will grow ~1 GB+ (native OCR stack price; Athena went 1.53→2.7 GB).
