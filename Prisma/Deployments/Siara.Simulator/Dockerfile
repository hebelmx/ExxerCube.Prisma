# Build context: repo root (ExxerCube.Prisma/).
# docker build -f "Prisma/Deployments/Siara.Simulator/Dockerfile" -t prisma-siara-simulator .
#
# The Siara.Simulator is a standalone Blazor Server app shipped as a pre-built
# publish output under Prisma/Deployments/Siara.Simulator/app/.
# This Dockerfile wraps that artifact in the aspnet:10.0 runtime image.
# No compile step is needed here — the binary is already built.
#
# Port: the container listens on 8080 (HTTP only; ASPNETCORE_URLS override at
# runtime). TLS is not needed for an internal dev-stack simulator service.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# HTTP port only — SIARA Simulator is an internal dev-stack tool; TLS is
# terminated at the edge or handled by the host when needed.
EXPOSE 8080

# All config must be supplied at runtime via environment variables.
# Examples (do NOT hardcode values here):
#   ASPNETCORE_URLS=http://+:8080
#   ASPNETCORE_ENVIRONMENT=Development
#   Serilog__WriteTo__2__Args__serverUrl=http://seq:5341
#   SIARA_LOG_PATH=logs/siara-.log

COPY Prisma/Deployments/Siara.Simulator/app/ .

ENTRYPOINT ["dotnet", "Siara.Simulator.dll"]
