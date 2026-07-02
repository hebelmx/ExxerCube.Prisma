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
    "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" \
    -p:ArtifactsBaseDir=/tmp/build/

# Publish Release build. -o /app/publish sets PublishDir regardless of OutputPath.
RUN dotnet publish \
    "Prisma/Code/Src/CSharp/07 UI/UI/ExxerCube.Prisma.Web.UI/ExxerCube.Prisma.Web.UI.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:ArtifactsBaseDir=/tmp/build/

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

COPY --from=build /app/publish .

# GH#23: bake the PRP1 fixture set into the image for the /document-processing demo.
# FixtureFinder resolves fixtures by walking up from CWD / AppDomain.BaseDirectory
# (both = WORKDIR /app here) looking for a "Fixtures" dir, so this lands them where
# it looks. The Prisma/Fixtures tree is otherwise excluded by .dockerignore, which
# re-includes just Prisma/Fixtures/PRP1/** for this COPY.
COPY Prisma/Fixtures/PRP1/ ./Fixtures/PRP1/

# GH#24: install the Chromium build Playwright needs for the /browser-automation demo.
# Use the bundled Playwright CLI (node + cli.js from the publish output) so the browser
# version matches the pinned Microsoft.Playwright exactly (no hard-coded build number).
# The `node/*/node` glob picks the arch-specific node bundle (linux-x64 / linux-arm64)
# so the build is not pinned to one architecture. --with-deps installs the OS libraries
# Chromium needs (runtime is Ubuntu 24.04 noble).
RUN /app/.playwright/node/*/node /app/.playwright/package/cli.js install --with-deps chromium

ENTRYPOINT ["dotnet", "ExxerCube.Prisma.Web.UI.dll"]
