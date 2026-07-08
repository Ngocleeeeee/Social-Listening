FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BrandRadar.Shared/BrandRadar.Shared.csproj BrandRadar.Shared/
COPY Dashboard.Api/Dashboard.Api.csproj Dashboard.Api/
RUN dotnet restore Dashboard.Api/Dashboard.Api.csproj
COPY BrandRadar.Shared/ BrandRadar.Shared/
COPY Dashboard.Api/ Dashboard.Api/
RUN dotnet publish Dashboard.Api/Dashboard.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
  CMD curl -f http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Dashboard.Api.dll"]
