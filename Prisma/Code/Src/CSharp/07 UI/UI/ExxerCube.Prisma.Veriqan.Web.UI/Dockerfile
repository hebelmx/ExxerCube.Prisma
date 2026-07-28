# Build context: repo root (ExxerCube.Prisma/).
# docker build -f "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/Dockerfile" -t veriqan-web-ui .
#
# Directory.Build.props sets ArtifactsBaseDir=E:\Dynamic\ExxerCubeBanamex\BuildArtifacts\Prisma\
# which is a host-only path. We override it with -p:ArtifactsBaseDir=/tmp/build/ so all derived
# MSBuild output paths (BaseOutputPath, OutputPath, BaseIntermediateOutputPath) resolve inside
# the container. The publish step uses -o /app/publish (PublishDir), which is independent and
# always lands in the correct location. (Mirrors 04 Services/Veriqan.Worker/Dockerfile.)
#
# PERSISTENCE DECISION (VLD-S1/S7, owner-settled): this container is IN-MEMORY ONLY — no SQL
# Server dependency. ConnectionStrings__VeriqanDb must NOT be set; when absent, AddVeriqan falls
# back to AddVeriqanInMemoryPersistence(). Because persistence is in-memory, the
# Veriqan__LegalBaseline__EncryptionKey (AES-256, required only when SQL persistence is enabled)
# is NOT required for this image either. This is a sales-demo host, not a system of record.

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
    "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/ExxerCube.Prisma.Veriqan.Web.UI.csproj" \
    -p:ArtifactsBaseDir=/tmp/build/

# Publish Release build. -o /app/publish sets PublishDir regardless of OutputPath.
# wwwroot (including wwwroot/demo-fixtures, the /live demo card PDFs) is published automatically.
RUN dotnet publish \
    "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Veriqan.Web.UI/ExxerCube.Prisma.Veriqan.Web.UI.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:ArtifactsBaseDir=/tmp/build/

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The live pipeline (VLD-S5) rasterizes the marked-page hero PNG via PDFtoImage (PDFium) +
# SkiaSharp. PDFium's native library ships through the NuGet runtime package, but SkiaSharp's
# native library needs fontconfig/freetype present on a clean Debian aspnet base image, or glyph
# shaping fails at load time. Without these, hero rasterization degrades BEST-EFFORT (placeholder
# image, verdict still renders) rather than crashing — but we want the real hero, so install them.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 \
    && rm -rf /var/lib/apt/lists/*

# HTTP port — Veriqan Web.UI exposes /health, /, /live, /document-processing, /hybrid-extraction.
EXPOSE 8080

# All sensitive config must be supplied at runtime via environment variables.
# The double-underscore convention maps to JSON config nesting (ASP.NET Core standard).
#
# Required:
#   Veriqan__CsvReferenceData__RootDirectory — Absolute path to the mounted reference-bundle
#                                           directory (must match the volume mount point — see
#                                           compose file). Must be an ABSOLUTE path in-container:
#                                           the repo-root-relative default in appsettings.json
#                                           (resolved via a CLAUDE.md walk-up at dev time) cannot
#                                           resolve inside a published image with no repo tree, so
#                                           an absolute override here is mandatory, not optional.
#
# Deliberately OMITTED (in-memory demo decision — do NOT set these for this image):
#   ConnectionStrings__VeriqanDb           — Omitted => AddVeriqanInMemoryPersistence() fallback.
#   Veriqan__LegalBaseline__EncryptionKey  — Only required when SQL persistence is enabled.
#
# Optional / observability (safe defaults if unset):
#   ASPNETCORE_URLS                       — Override listen URL (default: http://+:8080)
#   ASPNETCORE_ENVIRONMENT                — Default: Development
#   Veriqan__OtlpEndpoint                 — OTLP gRPC endpoint (default: http://localhost:4317)
#   Veriqan__Seq__ServerUrl               — Seq structured-log server URL (empty = disabled)
#   Veriqan__Smtp__Host / __Port / ...    — SMTP relay for alert emails. The live pipeline logs a
#                                           harmless best-effort SMTP failure when no relay is
#                                           configured — expected in a standalone demo, not fatal.

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ExxerCube.Prisma.Veriqan.Web.UI.dll"]
