FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BrandRadar.Shared/BrandRadar.Shared.csproj BrandRadar.Shared/
COPY Analysis.Worker/Analysis.Worker.csproj Analysis.Worker/
RUN dotnet restore Analysis.Worker/Analysis.Worker.csproj
COPY BrandRadar.Shared/ BrandRadar.Shared/
COPY Analysis.Worker/ Analysis.Worker/
RUN dotnet publish Analysis.Worker/Analysis.Worker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Analysis.Worker.dll"]
