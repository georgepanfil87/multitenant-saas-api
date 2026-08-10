# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy only the project files first and restore separately. Docker layers invalidate in
# order, so as long as the NuGet dependencies are unchanged the slow restore stays cached
# even after a code change.
COPY global.json Directory.Build.props ./
COPY src/MultiTenantSaaS.Domain/*.csproj          src/MultiTenantSaaS.Domain/
COPY src/MultiTenantSaaS.Application/*.csproj     src/MultiTenantSaaS.Application/
COPY src/MultiTenantSaaS.Infrastructure/*.csproj  src/MultiTenantSaaS.Infrastructure/
COPY src/MultiTenantSaaS.Api/*.csproj             src/MultiTenantSaaS.Api/
RUN dotnet restore src/MultiTenantSaaS.Api/MultiTenantSaaS.Api.csproj

COPY src/ src/
RUN dotnet publish src/MultiTenantSaaS.Api/MultiTenantSaaS.Api.csproj \
        --configuration Release \
        --no-restore \
        --output /app

# ---------- Final stage ----------
# Runtime image without the SDK: no compiler and no sources in what ships to production.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Non-root user, predefined in the .NET 8 images: a process escape does not get root.
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "MultiTenantSaaS.Api.dll"]
