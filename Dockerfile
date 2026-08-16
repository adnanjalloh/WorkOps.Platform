# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS build
ARG VCS_REF=local
ARG VERSION=0.1.0-local
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props WorkOps.Platform.slnx ./
COPY src/WorkOps.Api/WorkOps.Api.csproj src/WorkOps.Api/
COPY src/WorkOps.Application/WorkOps.Application.csproj src/WorkOps.Application/
COPY src/WorkOps.Contracts/WorkOps.Contracts.csproj src/WorkOps.Contracts/
COPY src/WorkOps.Domain/WorkOps.Domain.csproj src/WorkOps.Domain/
COPY src/WorkOps.Infrastructure/WorkOps.Infrastructure.csproj src/WorkOps.Infrastructure/
COPY src/WorkOps.Api/packages.lock.json src/WorkOps.Api/
COPY src/WorkOps.Application/packages.lock.json src/WorkOps.Application/
COPY src/WorkOps.Contracts/packages.lock.json src/WorkOps.Contracts/
COPY src/WorkOps.Domain/packages.lock.json src/WorkOps.Domain/
COPY src/WorkOps.Infrastructure/packages.lock.json src/WorkOps.Infrastructure/
RUN dotnet restore src/WorkOps.Api/WorkOps.Api.csproj --locked-mode

COPY . .
RUN dotnet publish src/WorkOps.Api/WorkOps.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false /p:Version="$VERSION" /p:SourceRevisionId="$VCS_REF"

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble-chiseled AS runtime
ARG VCS_REF=local
ARG VERSION=0.1.0-local
LABEL org.opencontainers.image.source="https://github.com/adnanjalloh/WorkOps.Platform" \
      org.opencontainers.image.revision="$VCS_REF" \
      org.opencontainers.image.version="$VERSION"
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "WorkOps.Api.dll"]
