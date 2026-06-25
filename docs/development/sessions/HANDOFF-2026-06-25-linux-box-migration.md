# Handoff — moved the dev workload to a new Linux box (2026-06-25)

Heads up if you're the next one to pick this up: **the development environment changed.**
Up to now Prisma was being built and run hands-off on a Windows machine. That box's Docker
stopped working and didn't come back after repeated repairs/reinstalls, so rather than keep
fighting it we gave up on it and **moved development to a fresh Linux workstation** — Ubuntu
26.04 LTS, where Docker runs fine. This repo was re-cloned there from scratch and we're
continuing on branch **`Liv`**.

Nothing about the product or the architecture changed. What changed is the host OS, and a
handful of things that were quietly Windows-only had to be made cross-platform so a clean
clone actually builds and tests on Linux. Those fixes are committed on `Liv` (and pushed) —
please don't revert them thinking they're stray:

- **Build artifacts path.** `Directory.Build.props` hard-coded `E:\Dynamic\…`. On Linux that
  got treated as a *relative* folder and created junk dirs that broke the build. It's now
  OS-conditional: Windows still uses `E:\`, Linux uses the repo-sibling
  `IndFusion/BuildArtifacts/Prisma/`. Override with the `ArtifactsBaseDir` env var if needed.
- **Tesseract.** `apt install tesseract-ocr` gives 5.5 / leptonica 1.86, but the charlesw
  wrapper P/Invokes the older Ubuntu-22.04 sonames. A small Linux-only post-build target
  (`eng/link-tesseract-natives.sh`) symlinks the system libs to the names it wants. Real-OCR
  is green (164/164). Export `TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata/` before the
  OCR / max-fidelity gate tier.
- **Emgu.CV / OpenCV.** This was the hard one — the old `ubuntu-x64` Emgu runtime stopped at
  4.12 and needs soversions 26.04 doesn't ship. Fixed by going to **Emgu.CV 4.13.0.5924** with
  the distro-pinned **`Emgu.CV.runtime.ubuntu-26.04-x64`** package, plus `UseRidGraph=true` on
  Linux and a base-dir symlink for `libcvextern.so`. 4.13 added a `bitShift` arg to
  `CvInvoke.CLAHE` — the one call site passes `0` (classic 8-bit behaviour). It needs these
  system libs once: `sudo apt install libvtk9.5 libhdf5-310 libavif16 libgeotiff5 liblapack3`.
  Imaging suite is green (200/200). *If this box's Ubuntu version ever changes, swap the runtime
  package to the matching `ubuntu-XX.XX-x64` and re-check `ldd libcvextern.so`.*
- **SQL Server.** Retired 2022; the Testcontainers fixtures now pull
  `mcr.microsoft.com/mssql/server:2025-latest`. `Tests.System.Storage` is green (69/69) — but
  run the heavy Docker tiers **uncontended** (don't kick off two container-heavy suites at once;
  the SQL container's health-check will time out under load).

How to build/test here is unchanged except for the platform shims above: from the repo root,
plain `dotnet build` / `dotnet test <csproj>` (the root `global.json` opts into MTP — don't
pass `--no-build`, MTP discovers 0 tests with the redirected output). A clean clone builds 0/0;
Domain 195/195, OCR 164/164, Imaging 200/200, SQL 69/69 all pass on this box.

That's it — the work continues exactly where it was, just on Linux now.
