# syntax=docker/dockerfile:1.7

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.400-noble
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.11-noble

FROM ${DOTNET_SDK_IMAGE} AS restore

WORKDIR /source

# Build the API and its dependencies, without the local Aspire AppHost.
COPY v2/Directory.Build.props ./v2/
COPY v2/src/Tenant/Tenant.Api/Tenant.Api.csproj ./v2/src/Tenant/Tenant.Api/
COPY v2/src/Tenant/Tenant.Application/Tenant.Application.csproj ./v2/src/Tenant/Tenant.Application/
COPY v2/src/Tenant/Tenant.Domain/Tenant.Domain.csproj ./v2/src/Tenant/Tenant.Domain/
COPY v2/src/Tenant/Tenant.Infrastructure/Tenant.Infrastructure.csproj ./v2/src/Tenant/Tenant.Infrastructure/
COPY v2/src/ServiceDefaults/ServiceDefaults.csproj ./v2/src/ServiceDefaults/
COPY v2/src/Platform.Shared/Platform.Shared.csproj ./v2/src/Platform.Shared/

RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore v2/src/Tenant/Tenant.Api/Tenant.Api.csproj

FROM restore AS build

COPY v2/src/Tenant/ ./v2/src/Tenant/
COPY v2/src/ServiceDefaults/ ./v2/src/ServiceDefaults/
COPY v2/src/Platform.Shared/ ./v2/src/Platform.Shared/

RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet build v2/src/Tenant/Tenant.Api/Tenant.Api.csproj \
      --configuration Release \
      --no-restore

# No test projects exist yet, so this currently verifies the release build.
FROM build AS test

FROM build AS publish

RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish v2/src/Tenant/Tenant.Api/Tenant.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      /p:UseAppHost=false

# Build a one-shot migrator from exactly the same source as the API.
FROM build AS migrations

RUN mkdir -p /app \
    && dotnet tool install dotnet-ef --tool-path /tools --version 10.0.11

# Bundle generation needs a configured DbContext, but never connects to a DB.
# Use a non-secret 32-byte placeholder key only for model construction in this command.
# The deployed API and migration runner must receive the real key at runtime.
RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    ConnectionStrings__Default="Host=localhost;Database=bundle_build_only" \
    Encryption__Key="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=" \
    /tools/dotnet-ef migrations bundle \
      --project v2/src/Tenant/Tenant.Infrastructure/Tenant.Infrastructure.csproj \
      --startup-project v2/src/Tenant/Tenant.Api/Tenant.Api.csproj \
      --context TenantDbContext \
      --configuration Release \
      --no-build \
      --output /app/efbundle

FROM ${DOTNET_ASPNET_IMAGE} AS tenant-final

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN mkdir -p /var/lib/strnadi/storage \
    && chown -R "$APP_UID:$APP_UID" /var/lib/strnadi

COPY --from=publish /app/publish/ ./
COPY --from=migrations --chmod=755 /app/efbundle /app/efbundle

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet-bundle

EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD bash -ec 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /utils/health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "Tenant.Api.dll"]

FROM ${DOTNET_SDK_IMAGE} AS administration-restore
WORKDIR /source
COPY v2/Directory.Build.props ./v2/
COPY v2/src/Administration/Administration.Api/Administration.Api.csproj ./v2/src/Administration/Administration.Api/
COPY v2/src/Administration/Administration.Application/Administration.Application.csproj ./v2/src/Administration/Administration.Application/
COPY v2/src/Administration/Administration.Domain/Administration.Domain.csproj ./v2/src/Administration/Administration.Domain/
COPY v2/src/Administration/Administration.Infrastructure/Administration.Infrastructure.csproj ./v2/src/Administration/Administration.Infrastructure/
COPY v2/src/ServiceDefaults/ServiceDefaults.csproj ./v2/src/ServiceDefaults/
COPY v2/src/Platform.Shared/Platform.Shared.csproj ./v2/src/Platform.Shared/
RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore v2/src/Administration/Administration.Api/Administration.Api.csproj

FROM administration-restore AS administration-build
COPY v2/src/Administration/ ./v2/src/Administration/
COPY v2/src/ServiceDefaults/ ./v2/src/ServiceDefaults/
COPY v2/src/Platform.Shared/ ./v2/src/Platform.Shared/
RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet build v2/src/Administration/Administration.Api/Administration.Api.csproj \
      --configuration Release --no-restore

FROM administration-build AS administration-test

FROM administration-build AS administration-publish
RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish v2/src/Administration/Administration.Api/Administration.Api.csproj \
      --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM administration-build AS administration-migrations
RUN mkdir -p /app \
    && dotnet tool install dotnet-ef --tool-path /tools --version 10.0.11
RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    ConnectionStrings__Default="Host=localhost;Database=bundle_build_only" \
    Encryption__Key="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=" \
    /tools/dotnet-ef migrations bundle \
      --project v2/src/Administration/Administration.Infrastructure/Administration.Infrastructure.csproj \
      --startup-project v2/src/Administration/Administration.Api/Administration.Api.csproj \
      --context AdminDbContext --configuration Release --no-build --output /app/efbundle

FROM ${DOTNET_ASPNET_IMAGE} AS administration-final
WORKDIR /app
RUN mkdir -p /var/lib/strnadi/keys /var/lib/strnadi/storage \
    && chown -R "$APP_UID:$APP_UID" /var/lib/strnadi
COPY --from=administration-publish /app/publish/ ./
COPY --from=administration-migrations --chmod=755 /app/efbundle /app/efbundle
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/dotnet-bundle
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD bash -ec 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /utils/health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "'
ENTRYPOINT ["dotnet", "Administration.Api.dll"]

# Keep existing Tenant jobs and plain docker builds compatible.
FROM tenant-final AS final
