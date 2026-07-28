# Build context: repo root (ExxerCube.Prisma/).
# docker build -f "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/Dockerfile" -t prisma-webui .
#
# Directory.Build.props sets ArtifactsBaseDir=E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\
# which is a host-only path. We override it with -p:ArtifactsBaseDir=/tmp/build/ so all derived
# MSBuild output paths (BaseOutputPath, OutputPath, BaseIntermediateOutputPath) resolve inside
# the container. The publish step uses -o /app/publish (PublishDir), which is independent and
# always lands in the correct location.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# O7: .dockerignore excludes .git/, so MinVer (git-tag-driven SemVer, wired in Directory.Build.props)
# is blind inside this build — it would fall back to its 0.0.0-alpha default. MinVerVersionOverride
# bypasses git entirely when set, so the caller (compose `build.args` / CI `--build-arg`) supplies the
# real version (e.g. `git describe --tags --always`); the 0.0.0-docker default keeps ad-hoc
# `docker build` invocations (no --build-arg) working without failing.
ARG APP_VERSION=0.0.0-docker
ENV MinVerVersionOverride=${APP_VERSION}

# Copy central package management, build props, and NuGet config so dotnet restore works.
# These files sit at Prisma/Code/Src/CSharp/ in the repo and are needed by every project.
COPY Prisma/Code/Src/CSharp/Directory.Build.props ./Prisma/Code/Src/CSharp/
COPY Prisma/Code/Src/CSharp/Directory.Packages.props ./Prisma/Code/Src/CSharp/
COPY nuget.config ./

# Copy the entire CSharp source tree. The solution has ~70 projects with a deep ProjectReference
# graph; copying the full tree is the simplest way to guarantee dotnet restore resolves all deps.
COPY Prisma/Code/Src/CSharp/ ./Prisma/Code/Src/CSharp/

# O1 follow-up (docs/planning-artifacts/remediation/TRACKER-webui-container-ocr.md): the repo's dev
# box is Ubuntu 26.04, so Infrastructure.Imaging.csproj defaults to Emgu.CV.runtime.ubuntu-26.04-x64
# — but this image's runtime stage (below) is aspnet:10.0 = Ubuntu 24.04 (noble), whose libcvextern
# needs VTK 9.1/HDF5 103 instead of 26.04's VTK 9.5/HDF5 310. Override the MSBuild property on BOTH
# restore (so the right package is resolved) and publish (so the right native asset is copied to
# publish/). Web.UI references Infrastructure.Imaging directly (EmguCvImageQualityAnalyzer, used by
# /document-processing quality analysis), so it has the identical Emgu hole Athena had (O1).
RUN dotnet restore \
    "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" \
    -p:ArtifactsBaseDir=/tmp/build/ \
    -p:EmguLinuxRuntimePackage=Emgu.CV.runtime.ubuntu-24.04-x64

# Publish Release build. -o /app/publish sets PublishDir regardless of OutputPath.
RUN dotnet publish \
    "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:ArtifactsBaseDir=/tmp/build/ \
    -p:EmguLinuxRuntimePackage=Emgu.CV.runtime.ubuntu-24.04-x64

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# HTTP ports — Blazor Server UI serves on 8080 (HTTP); configure TLS termination at ingress.
EXPOSE 8080

# All sensitive config must be supplied at runtime via environment variables.
# Examples (do NOT hardcode values here):
#   ConnectionStrings__DefaultConnection
#   Jwt__Secret
#   Siara__BaseUrl
#   Ember__HubUrl
#   Storage__BasePath
#   ASPNETCORE_URLS=http://+:8080

# GH#28 + O1 follow-up (TRACKER-webui-container-ocr.md): install the Tesseract OCR native stack for
# the /document-processing PDF-OCR path, plus the Emgu.CV noble system deps (mirrors the Athena
# Worker Dockerfile's apt layer exactly — same list, same rationale). This apt layer is
# code-independent, so it sits BEFORE the publish COPY below — otherwise every source edit would
# invalidate the ~1 GB package layer.
#
# Tesseract: the Tesseract.NET (charlesw) NuGet ships ONLY Windows natives (x64/tesseract50.dll,
# x64/leptonica-1.82.0.dll) and on Linux P/Invokes the sonames `libtesseract50.so` /
# `libleptonica-1.82.0.so` — which apt does not provide (noble ships libtesseract.so.5 /
# liblept.so.5/liblept.so.6). So: (1) apt-install tesseract + spa/eng language data (tessdata lands
# at /usr/share/tesseract-ocr/5/tessdata, which TesseractOcrExecutor already probes); (2) run the
# repo's link shim (below, after the publish COPY) to symlink the ABI-compatible system libs to the
# wrapper's expected names under /app/x64. Without this, PDF OCR fails "OCR failed for all pages".
#
# Emgu.CV: the ubuntu-24.04-x64 native package (selected at build time above) ships libcvextern.so
# under runtimes/ubuntu-x64/native/ — a more-specific RID than the portable linux-x64 the .NET host
# resolves at, so the default probing never finds it (see eng/link-emgu-native.sh for the RID-graph
# detail). The system packages below are libcvextern.so's own shared-library dependencies, the exact
# same list discovered empirically for the Athena Worker image (O1) via
# `ldd /app/runtimes/*/native/libcvextern.so` and iterated until clean (noble sonames differ from the
# 26.04 dev box this project is normally built on).
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-spa tesseract-ocr-eng \
        libvtk9.1t64 libhdf5-103-1t64 libavif16 libgeotiff5 liblapack3 \
        libgtk-3-0t64 libopenexr-3-1-30 libgdcm3.0t64 libnetcdf19t64 \
        libavcodec60 libavformat60 libavutil58 libswscale7 \
        libgstreamer1.0-0 libgstreamer-plugins-base1.0-0 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# The link shims must run AFTER the publish COPY: each gates on files in /app (the tesseract shim on
# x64/leptonica-1.82.0.dll, the emgu shim on runtimes/*/native/libcvextern.so — note its
# trailing-slash argument convention).
COPY "Prisma/Code/Src/CSharp/eng/link-tesseract-natives.sh" /tmp/link-tesseract-natives.sh
RUN bash /tmp/link-tesseract-natives.sh /app && rm /tmp/link-tesseract-natives.sh
COPY "Prisma/Code/Src/CSharp/eng/link-emgu-native.sh" /tmp/link-emgu-native.sh
RUN bash /tmp/link-emgu-native.sh /app/ && rm /tmp/link-emgu-native.sh

# GH#23: bake the PRP1 fixture set into the image for the /document-processing demo.
# FixtureFinder resolves fixtures by walking up from CWD / AppDomain.BaseDirectory
# (both = WORKDIR /app here) looking for a "Fixtures" dir, so this lands them where
# it looks. The Prisma/Fixtures tree is otherwise excluded by .dockerignore, which
# re-includes just Prisma/Fixtures/PRP1/** for this COPY.
COPY Prisma/Fixtures/PRP1/ ./Fixtures/PRP1/

# GH#29: bake the bulk-document sample set (pdf/docx/xml) so the /document-processing
# "Batch processing" demo can locate it. FixtureFinder.FindBulkDocumentsPath walks up from
# WORKDIR (/app) looking for a child "bulk_generated_documents_all_formats" dir, so land it
# directly under /app. Without this the demo errors "Failed to locate bulk documents directory".
# (The adaptive-extractor DOCX fixtures themselves already ship via the PRP1 COPY above; GH#29
# also fixes AdaptiveDocxFixtureService's path resolution to find them at /app/Fixtures/PRP1.)
COPY "Prisma/Deployments/Siara.Simulator/bulk_generated_documents_all_formats/" ./bulk_generated_documents_all_formats/

# GH#24: install the Chromium build Playwright needs for the /browser-automation demo.
# Use the bundled Playwright CLI (node + cli.js from the publish output) so the browser
# version matches the pinned Microsoft.Playwright exactly (no hard-coded build number).
# The `node/*/node` glob picks the arch-specific node bundle (linux-x64 / linux-arm64)
# so the build is not pinned to one architecture. --with-deps installs the OS libraries
# Chromium needs (runtime is Ubuntu 24.04 noble).
RUN /app/.playwright/node/*/node /app/.playwright/package/cli.js install --with-deps chromium

ENTRYPOINT ["dotnet", "ExxerCube.Prisma.Web.UI.dll"]
