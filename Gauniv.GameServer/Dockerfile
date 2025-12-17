# Stage 1: Base runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copier les fichiers csproj et restaurer
COPY ["Gauniv.GameServer/Gauniv.GameServer.csproj", "Gauniv.GameServer/"]
RUN dotnet restore "Gauniv.GameServer/Gauniv.GameServer.csproj"

# Copier tout le code source
COPY . .
WORKDIR "/src/Gauniv.GameServer"

# Build le projet
RUN dotnet build "Gauniv.GameServer.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "Gauniv.GameServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Variables d'environnement par défaut
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Gauniv.GameServer.dll"]
