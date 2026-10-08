# =======================================================
# Etapa 1: Runtime base liviano para ejecución (.NET 8)
# =======================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
ENV ASPNETCORE_HTTP_PORTS=8080

# =======================================================
# Etapa 2: Entorno SDK para compilación y restore (.NET 8)
# =======================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Optimización de caché de capas en Docker:
# Copiamos primero la solución y los archivos .csproj para restaurar dependencias
COPY ["Dsw2025Tpi.sln", "./"]
COPY ["Dsw2025Tpi.Api/Dsw2025Tpi.Api.csproj", "Dsw2025Tpi.Api/"]
COPY ["Dsw2025Tpi.Application/Dsw2025Tpi.Application.csproj", "Dsw2025Tpi.Application/"]
COPY ["Dsw2025Tpi.Data/Dsw2025Tpi.Data.csproj", "Dsw2025Tpi.Data/"]
COPY ["Dsw2025Tpi.Domain/Dsw2025Tpi.Domain.csproj", "Dsw2025Tpi.Domain/"]

RUN dotnet restore "Dsw2025Tpi.sln"

# Copiamos el resto del código fuente del proyecto
COPY . .
WORKDIR "/src/Dsw2025Tpi.Api"
RUN dotnet build "Dsw2025Tpi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# =======================================================
# Etapa 3: Publicación de artefactos optimizados
# =======================================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Dsw2025Tpi.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# =======================================================
# Etapa 4: Imagen final de producción (Runtime liviano)
# =======================================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Dsw2025Tpi.Api.dll"]
