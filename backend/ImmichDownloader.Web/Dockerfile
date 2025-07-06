# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["ImmichDownloader.sln", "./"]
COPY ["ImmichDownloader.Web/ImmichDownloader.Web.csproj", "ImmichDownloader.Web/"]
COPY ["Immich.Data/Immich.Data.csproj", "Immich.Data/"]

# Restore dependencies
RUN dotnet restore "ImmichDownloader.Web/ImmichDownloader.Web.csproj"

# Copy everything else
COPY . .

# Build the application
WORKDIR /src/ImmichDownloader.Web
RUN dotnet build -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser -u 5678 appuser

WORKDIR /app

# Copy published output
COPY --from=publish /app/publish .

# Create necessary directories with proper permissions
RUN mkdir -p /app/data/downloads /app/data/resized && \
    chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5000

# Run the application
ENTRYPOINT ["dotnet", "ImmichDownloader.Web.dll"]