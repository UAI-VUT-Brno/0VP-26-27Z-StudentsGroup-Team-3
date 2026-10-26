# =============================================================================
# UAI.ZeroVP – ToDo API
# Multi-stage build: SDK image for restore/publish, slim ASP.NET runtime image
# for the final container. Build from the repository root:
#
#   docker build -t uai-zerovp-api .
# =============================================================================

# ---------- Build stage -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first so the restore layer stays cached
# until dependencies change.
COPY src/UAI.ZeroVP.DTO/UAI.ZeroVP.DTO.csproj   src/UAI.ZeroVP.DTO/
COPY src/UAI.ZeroVP.Data/UAI.ZeroVP.Data.csproj src/UAI.ZeroVP.Data/
COPY src/UAI.ZeroVP.API/UAI.ZeroVP.API.csproj   src/UAI.ZeroVP.API/
RUN dotnet restore src/UAI.ZeroVP.API/UAI.ZeroVP.API.csproj

# Copy the sources needed for the API (tests are not part of the image).
COPY src/ src/
RUN dotnet publish src/UAI.ZeroVP.API/UAI.ZeroVP.API.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- Runtime stage -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Directory for the shared JSON storage (mounted as a Docker volume).
# Created as root and handed over to the non-root "app" user so that
# a fresh named volume inherits the correct ownership.
RUN mkdir -p /data && chown app:app /data

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    TodoStorage__FilePath=/data/todos.json

COPY --from=build /app/publish .

USER app
EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "UAI.ZeroVP.API.dll"]