# ─── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching)
COPY ["src/Api/ObservabilityLab.Api.csproj",                     "src/Api/"]
COPY ["src/Application/ObservabilityLab.Application.csproj",     "src/Application/"]
COPY ["src/Infrastructure/ObservabilityLab.Infrastructure.csproj","src/Infrastructure/"]
COPY ["src/Observability/ObservabilityLab.Observability.csproj",  "src/Observability/"]
COPY ["src/Alerting/ObservabilityLab.Alerting.csproj",           "src/Alerting/"]
COPY ["src/BuildingBlocks/ObservabilityLab.BuildingBlocks.csproj","src/BuildingBlocks/"]
COPY ["src/Domain/ObservabilityLab.Domain.csproj",               "src/Domain/"]

RUN dotnet restore "src/Api/ObservabilityLab.Api.csproj"

# Copy source
COPY src/ src/

# Publish
WORKDIR /src/src/Api
RUN dotnet publish "ObservabilityLab.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create logs directory
RUN mkdir -p /app/logs

# Non-root user for security
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup --shell /bin/sh appuser
RUN chown -R appuser:appgroup /app

USER appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

EXPOSE 8080
EXPOSE 8443

ENTRYPOINT ["dotnet", "ObservabilityLab.Api.dll"]