#!/bin/bash

# Start both backend and frontend in development mode

echo "Starting Immich Downloader Development Environment..."

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed"
    exit 1
fi

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo "Error: Node.js is not installed"
    exit 1
fi

# Kill any existing processes on our ports
echo "Cleaning up existing processes..."
lsof -ti:5000 | xargs kill -9 2>/dev/null || true
lsof -ti:3000 | xargs kill -9 2>/dev/null || true

# Start backend
echo "Starting backend on http://localhost:5000..."
cd backend
dotnet run --project ImmichDownloader.Web &
BACKEND_PID=$!

# Wait for backend to start
echo "Waiting for backend to start..."
sleep 5

# Start frontend
echo "Starting frontend on http://localhost:3000..."
cd ../frontend
npm install
npm start &
FRONTEND_PID=$!

echo ""
echo "Development environment started!"
echo "Backend: http://localhost:5000"
echo "Frontend: http://localhost:3000"
echo ""
echo "Press Ctrl+C to stop both services"

# Handle Ctrl+C
trap "echo 'Stopping services...'; kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" INT

# Wait for processes
wait