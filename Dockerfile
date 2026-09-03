# syntax=docker/dockerfile:1.7

ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.400-noble
ARG DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.11-noble

FROM ${DOTNET_SDK_IMAGE} AS restore

WORKDIR /source

COPY v2/Directory.Build.props v2/StrnadiAPI.v2.slnx ./v2/
COPY v2/Strnadi.Api/Strnadi.Api.csproj ./v2/Strnadi.Api/
COPY v2/Strnadi.Application/Strnadi.Application.csproj ./v2/Strnadi.Application/
COPY v2/Strnadi.Domain/Strnadi.Domain.csproj ./v2/Strnadi.Domain/
COPY v2/Strnadi.Infrastructure/Strnadi.Infrastructure.csproj ./v2/Strnadi.Infrastructure/

RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore v2/StrnadiAPI.v2.slnx

FROM restore AS build

COPY v2/ ./v2/

RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet build v2/StrnadiAPI.v2.slnx \
      --configuration Release \
      --no-restore

# No test projects exist yet, so this currently verifies the release build.
FROM build AS test

FROM build AS publish

RUN --mount=type=cache,id=strnadi-api-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish v2/Strnadi.Api/Strnadi.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish \
      /p:UseAppHost=false

FROM ${DOTNET_ASPNET_IMAGE} AS final

WORKDIR /app

RUN mkdir -p /var/lib/strnadi/storage \
    && chown -R "$APP_UID:$APP_UID" /var/lib/strnadi

COPY --from=publish /app/publish/ ./

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD bash -ec 'exec 3<>/dev/tcp/127.0.0.1/8080; printf "GET /utils/health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3; head -n 1 <&3 | grep -q " 200 "'

ENTRYPOINT ["dotnet", "Strnadi.Api.dll"]