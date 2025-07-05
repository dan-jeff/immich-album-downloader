# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Immich Downloader is a modern web application for downloading and resizing photos from Immich servers. It features a .NET 9 backend with ASP.NET Core and a React 18 TypeScript frontend, designed for memory-efficient processing of large photo albums.

## Common Development Commands

### Backend (.NET 9)
```bash
# Navigate to backend
cd backend

# Restore dependencies
dotnet restore

# Run database migrations
dotnet ef database update --project ImmichDownloader.Web

# Run backend in development
dotnet run --project ImmichDownloader.Web

# Run tests
dotnet test

# Create new migration
dotnet ef migrations add MigrationName --project ImmichDownloader.Web
```

### Frontend (React 18)
```bash
# Navigate to frontend
cd frontend

# Install dependencies
npm install

# Start development server
npm start

# Build for production
npm run build

# Run tests
npm test
```

### Development Environment
```bash
# Start both backend and frontend in development
./scripts/start-dev.sh

# Or use Docker Compose
docker-compose up -d

# Access application at http://localhost:8080
```

## Core Architecture

### Backend Services Architecture
- **Streaming Services**: `StreamingDownloadService` and `StreamingResizeService` for memory-efficient processing
- **Legacy Services**: `DownloadService` and `ResizeService` kept for backward compatibility
- **Background Processing**: `BackgroundTaskService` with `BackgroundTaskQueue` using .NET Channels
- **Real-time Communication**: `TaskProgressService` with SignalR for live progress updates
- **Authentication**: JWT-based with `AuthService` and BCrypt password hashing
- **External Integration**: `ImmichService` for Immich server communication

### Frontend Architecture
- **State Management**: React Context for authentication, local state for components
- **Real-time Updates**: Custom `useWebSocket` hook for SignalR integration
- **API Layer**: Centralized Axios client with JWT token interceptors
- **Responsive Design**: Bootstrap 5 with mobile-first approach
- **Component Organization**: Feature-based components with clear separation of concerns

### Key Design Patterns
- **Streaming Architecture**: Disk-based processing prevents memory issues with large albums
- **Background Task Processing**: Non-blocking operations with real-time progress tracking
- **Service Scope Management**: Proper DI scoping for database operations in singleton services
- **Error Handling**: Comprehensive exception handling with logging and user feedback

## Database Operations

The application uses Entity Framework Core with SQLite:
- Database context: `ApplicationDbContext`
- Migrations are in `backend/ImmichDownloader.Web/Migrations/`
- Database is auto-created on startup via `EnsureCreated()`

## Configuration

### Environment Variables
Configuration is managed through Docker Compose environment variables in `docker-compose.yml`:
- `JWT_SECRET_KEY`: Required 256-bit secret for JWT tokens
- `IMMICH_URL`: Immich server URL  
- `IMMICH_API_KEY`: Immich API key
- `CONNECTION_STRING`: Database connection (defaults to SQLite)
- `ALLOWED_ORIGINS`: CORS origins (comma-separated)

### Application Settings
- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development overrides
- `appsettings.Production.json`: Production overrides

## Key Technical Considerations

### Memory Management
- Use streaming services (`StreamingDownloadService`, `StreamingResizeService`) for large albums
- Chunked processing limits concurrent memory usage (50 images per chunk)
- Direct file I/O reduces garbage collection pressure

### Concurrency
- Background task queue is bounded (100 tasks maximum)
- Download concurrency is limited (max 5 simultaneous downloads)
- Use cancellation tokens for graceful shutdown

### Security
- JWT tokens with configurable expiration
- BCrypt password hashing with salt
- CORS configured for cross-origin requests
- Docker Compose environment variable configuration

## Testing

### Backend Testing
- Unit tests should use in-memory database for EF Core
- Mock external services (ImmichService) for integration tests
- Test streaming services with small datasets to avoid memory issues

### Frontend Testing
- Jest and React Testing Library are configured
- Test authentication flows with mock tokens
- Test real-time updates with SignalR mocks

## Deployment

### Docker Deployment
- Multi-stage Dockerfile for both backend and frontend
- Nginx reverse proxy configuration in `nginx.conf`
- Volume mounts for data persistence and downloads
- Environment variable configuration via Docker Compose

### Production Considerations
- Set `ASPNETCORE_ENVIRONMENT=Production`
- Use secure JWT secret keys (generate with `openssl rand -base64 32`)
- Configure proper CORS origins
- Set up volume mounts for persistent data and downloads

## Development Workflow Notes
- Use docker to run the api and ui