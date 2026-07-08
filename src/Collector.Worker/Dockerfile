FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BrandRadar.Shared/BrandRadar.Shared.csproj BrandRadar.Shared/
COPY Collector.Worker/Collector.Worker.csproj Collector.Worker/
RUN dotnet restore Collector.Worker/Collector.Worker.csproj
COPY BrandRadar.Shared/ BrandRadar.Shared/
COPY Collector.Worker/ Collector.Worker/
RUN dotnet publish Collector.Worker/Collector.Worker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Collector.Worker.dll"]
