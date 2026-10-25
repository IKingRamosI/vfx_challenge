# Build stage: full SDK to restore, build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/FxRates.Api/FxRates.Api.csproj -c Release -o /app/publish

# Runtime stage: only the ASP.NET runtime, smaller image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FxRates.Api.dll"]
