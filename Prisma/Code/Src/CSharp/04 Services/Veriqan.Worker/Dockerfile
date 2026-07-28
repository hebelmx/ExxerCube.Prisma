# Build context: repo root (ExxerCube.Prisma/).
# docker build -f "Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/Dockerfile" -t veriqan-worker .
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
# real version (e.g. `git describe --tags --always`, prefix-stripped — see quality-gates.yml). The
# ARG default (0.0.0-docker) only applies when --build-arg APP_VERSION is omitted entirely; an
# EXPLICITLY EMPTY --build-arg APP_VERSION= would otherwise expand to an empty
# MinVerVersionOverride, which MinVer treats as unset and silently degrades to 0.0.0-alpha.0 — so
# the ENV line repeats the same shell-style ":-" default as a second safety net for that case.
ARG APP_VERSION=0.0.0-docker
ENV MinVerVersionOverride=${APP_VERSION:-0.0.0-docker}

# Copy central package management, build props, and NuGet config so dotnet restore works.
# These files sit at Prisma/Code/Src/CSharp/ in the repo and are needed by every project.
COPY Prisma/Code/Src/CSharp/Directory.Build.props ./Prisma/Code/Src/CSharp/
COPY Prisma/Code/Src/CSharp/Directory.Packages.props ./Prisma/Code/Src/CSharp/
COPY nuget.config ./

# Copy the entire CSharp source tree. The solution has ~70 projects with a deep ProjectReference
# graph; copying the full tree is the simplest way to guarantee dotnet restore resolves all deps.
COPY Prisma/Code/Src/CSharp/ ./Prisma/Code/Src/CSharp/

# Restore using the host project. ArtifactsBaseDir override keeps all artifacts inside the container.
RUN dotnet restore \
    "Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/ExxerCube.Prisma.Veriqan.Worker.csproj" \
    -p:ArtifactsBaseDir=/tmp/build/

# Publish Release build. -o /app/publish sets PublishDir regardless of OutputPath.
RUN dotnet publish \
    "Prisma/Code/Src/CSharp/04 Services/Veriqan.Worker/ExxerCube.Prisma.Veriqan.Worker.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:ArtifactsBaseDir=/tmp/build/

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# HTTP port — Veriqan Worker exposes /health, /health/live, /health/ready, /verify, /batch.
EXPOSE 8080

# All sensitive config must be supplied at runtime via environment variables.
# The double-underscore convention maps to JSON config nesting (ASP.NET Core standard).
# Required variables (no defaults — container will start but warn/degrade without them):
#   ConnectionStrings__VeriqanDb          — SQL Server connection string for durable persistence.
#                                           When absent the worker falls back to in-memory mode.
#   Veriqan__CsvReferenceData__RootDirectory — Path to the mounted reference-bundle directory.
#                                           Must match the volume mount point (see compose file).
#   Veriqan__LegalBaseline__EncryptionKey — Base64-encoded 32-byte AES-256 key.
#                                           Required when SQL persistence is enabled.
# Optional / observability:
#   Veriqan__OtlpEndpoint                 — OTLP gRPC endpoint (default: http://localhost:4317)
#   Veriqan__Seq__ServerUrl               — Seq structured-log server URL (empty = disabled)
#   Veriqan__BatchProcessor__MaxConcurrency — Concurrent worker tasks (0 = ProcessorCount)
#   Veriqan__PdfExtraction__MaxSizeBytes  — Max PDF size in bytes (default: 52428800 = 50 MB)
#   Veriqan__PdfExtraction__ParseTimeoutSeconds — PDF parse timeout (default: 30)
#   Veriqan__Smtp__Host                   — SMTP relay host for alert emails
#   Veriqan__Smtp__Port                   — SMTP port (default in config: 25)
#   Veriqan__Smtp__EnableSsl              — true/false (default: false)
#   Veriqan__Smtp__From                   — Sender address for alert emails
#   Veriqan__Smtp__UserName               — SMTP auth user (null = unauthenticated relay)
#   Veriqan__Smtp__Password               — SMTP auth password (null = unauthenticated relay)
#   Veriqan__Alerts__Recipients           — JSON array of alert recipient addresses
#   ASPNETCORE_URLS                       — Override listen URL (default: http://+:8080)

COPY --from=build /app/publish .

# E7.S7.2/S7.3: install the Tesseract OCR native stack for the header-image product-name OCR
# stage (HeaderImageOcrStage / TesseractHeaderProductOcrEngine). The Tesseract.NET (charlesw)
# NuGet ships ONLY Windows natives (x64/tesseract50.dll, x64/leptonica-1.82.0.dll) and on Linux
# P/Invokes the sonames `libtesseract50.so` / `libleptonica-1.82.0.so` — which apt does not
# provide (noble ships libtesseract.so.5 / liblept.so.5). So: (1) apt-install tesseract + spa/eng
# language data (tessdata lands at /usr/share/tesseract-ocr/5/tessdata, which
# TesseractHeaderProductOcrEngine already probes); (2) run the repo's link shim to symlink the
# ABI-compatible system libs to the wrapper's expected names under /app/x64. Without this the
# container fails SILENTLY at runtime (mirrors the identical fix already applied to Prisma's
# Web.UI/Dockerfile for GH#28).
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-spa tesseract-ocr-eng \
    && rm -rf /var/lib/apt/lists/*
COPY "Prisma/Code/Src/CSharp/eng/link-tesseract-natives.sh" /tmp/link-tesseract-natives.sh
RUN bash /tmp/link-tesseract-natives.sh /app && rm /tmp/link-tesseract-natives.sh

ENTRYPOINT ["dotnet", "ExxerCube.Prisma.Veriqan.Worker.dll"]
