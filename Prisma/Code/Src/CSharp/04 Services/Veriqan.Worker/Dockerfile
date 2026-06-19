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

ENTRYPOINT ["dotnet", "ExxerCube.Prisma.Veriqan.Worker.dll"]
