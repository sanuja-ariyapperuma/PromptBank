# ── Stage 1: build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies first (layer-cache friendly)
COPY ["PromptBank/PromptBank.csproj", "PromptBank/"]
RUN dotnet restore "PromptBank/PromptBank.csproj"

# Copy the rest of the source and publish
COPY PromptBank/ PromptBank/
RUN dotnet publish "PromptBank/PromptBank.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create the data directory that will be used for the SQLite database file.
# The directory is owned by the non-root app user provided by the base image.
RUN mkdir -p /app/data && chown app:app /app/data

# Copy published output from the build stage
COPY --from=build --chown=app:app /app/publish .

# Persist the SQLite database on a mounted volume at /app/data
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/promptbank.db"

# ASP.NET Core 10 defaults to port 8080 in containers
EXPOSE 8080

# Run as the non-root 'app' user shipped by the base image
USER app

ENTRYPOINT ["dotnet", "PromptBank.dll"]
