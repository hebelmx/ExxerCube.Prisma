# Build context: repo root (ExxerCube.Prisma/).
# docker build -f "Prisma/Code/Src/CSharp/04 Services/Reconciliator/Prisma.Reconciliator.Worker/Dockerfile" -t prisma-reconciliator .
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

# Restore using the host project. ArtifactsBaseDir override keeps all artifacts inside the container.
RUN dotnet restore \
    "Prisma/Code/Src/CSharp/04 Services/Reconciliator/Prisma.Reconciliator.Worker/ExxerCube.Prisma.Reconciliator.Worker.csproj" \
    -p:ArtifactsBaseDir=/tmp/build/

# Publish Release build. -o /app/publish sets PublishDir regardless of OutputPath.
RUN dotnet publish \
    "Prisma/Code/Src/CSharp/04 Services/Reconciliator/Prisma.Reconciliator.Worker/ExxerCube.Prisma.Reconciliator.Worker.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:ArtifactsBaseDir=/tmp/build/

# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# HTTP port — Reconciliator Worker exposes /health, /health/live, /health/ready endpoints.
EXPOSE 8080

# All sensitive config must be supplied at runtime via environment variables.
# Examples (do NOT hardcode values here):
#   ConnectionStrings__DefaultConnection
#   Jwt__Secret
#   Storage__BasePath
#   ReconciliationClient__HubUrl  (Athena reconciliation hub)
#   ProcessIdentity__Clearance
#   ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ExxerCube.Prisma.Reconciliator.Worker.dll"]
