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
WORKDIR /app

# Install ImageSharp dependencies
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published output
COPY --from=publish /app/publish .

# React build will be served separately in production

# Create necessary directories
RUN mkdir -p downloads resized data

# Expose port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "ImmichDownloader.Web.dll"]