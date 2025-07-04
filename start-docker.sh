#!/bin/bash

# Start Immich Downloader with Docker

echo "🚀 Starting Immich Downloader..."

# Check if .env file exists
if [ ! -f ".env" ]; then
    echo "⚠️  No .env file found. Creating from template..."
    cp .env.example .env
    echo "📝 Please edit .env file with your configuration before running again."
    echo "   Required: JWT_SECRET_KEY, IMMICH_URL, IMMICH_API_KEY"
    exit 1
fi

# Load environment variables
set -a
source .env
set +a

# Validate required environment variables
if [ -z "$JWT_SECRET_KEY" ] || [ "$JWT_SECRET_KEY" = "your-very-long-secure-secret-key-at-least-32-characters-long" ]; then
    echo "❌ JWT_SECRET_KEY must be set in .env file"
    exit 1
fi

if [ -z "$IMMICH_URL" ] || [ "$IMMICH_URL" = "http://your-immich-server:2283" ]; then
    echo "❌ IMMICH_URL must be set in .env file"
    exit 1
fi

if [ -z "$IMMICH_API_KEY" ] || [ "$IMMICH_API_KEY" = "your-immich-api-key" ]; then
    echo "❌ IMMICH_API_KEY must be set in .env file"
    exit 1
fi

# Auto-detect host IP if not set
if [ -z "$HOST_IP" ]; then
    HOST_IP=$(hostname -I | awk '{print $1}')
    export HOST_IP
    echo "🔍 Auto-detected host IP: $HOST_IP"
fi

# Stop any existing containers
echo "🛑 Stopping existing containers..."
docker-compose down

# Build and start containers
echo "🔨 Building and starting containers..."
docker-compose up -d --build

# Wait for services to start
echo "⏳ Waiting for services to start..."
sleep 10

# Check if services are running
if docker-compose ps | grep -q "Up"; then
    echo "✅ Immich Downloader is running!"
    echo ""
    echo "🌐 Access the application at:"
    echo "   Local:    http://localhost:8080"
    echo "   Network:  http://$HOST_IP:8080"
    echo ""
    echo "📊 View logs with: docker-compose logs -f"
    echo "🛑 Stop with: docker-compose down"
else
    echo "❌ Failed to start services. Check logs with: docker-compose logs"
    exit 1
fi