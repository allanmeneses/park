# API Parking — imagem de produção (Azure Container Apps / ACR)
# Build: na raiz do repositório — docker build -t parking-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/src ./src
RUN dotnet restore src/Parking.Api/Parking.Api.csproj
RUN dotnet publish src/Parking.Api/Parking.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Parking.Api.dll"]
