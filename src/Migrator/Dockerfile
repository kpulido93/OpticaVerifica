FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Migrator/Migrator.csproj src/Migrator/
RUN dotnet restore src/Migrator/Migrator.csproj

COPY src/Migrator/ src/Migrator/
RUN dotnet publish src/Migrator/Migrator.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Migrator.dll"]
