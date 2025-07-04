# ImmichDownloader.Web - .NET 9 Implementation

This is a complete .NET 9 rewrite of the Python Immich Downloader application, providing the same functionality with improved performance and type safety.

## Features

- ✅ Full async/await implementation with ASP.NET Core 9
- ✅ Entity Framework Core 9 with SQLite database
- ✅ JWT-based authentication with BCrypt password hashing
- ✅ SignalR for real-time progress updates
- ✅ ImageSharp for cross-platform image processing with HEIC support
- ✅ Background task processing with IHostedService
- ✅ RESTful API matching the Python implementation
- ✅ React frontend integration

## Prerequisites

- .NET 9 SDK
- Node.js (for building React frontend)

## Development

### Run the application

```bash
cd ImmichDownloader.Web
dotnet run
```

The application will be available at http://localhost:5000

### Build for production

```bash
dotnet publish -c Release -o ./publish
```

### Docker

```bash
# Build the Docker image
docker build -t immich-downloader-web .

# Run the container
docker run -p 8080:80 \
  -v $(pwd)/immich_downloader.db:/app/immich_downloader.db \
  -e Immich__Url=http://your-immich-server:2283 \
  -e Immich__ApiKey=your-api-key \
  immich-downloader-web
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=immich_downloader.db"
  },
  "Jwt": {
    "Key": "your-secret-key-change-this-in-production",
    "ExpireMinutes": 30
  },
  "Immich": {
    "Url": "http://your-immich-server:2283",
    "ApiKey": "your-api-key"
  }
}
```

### Environment Variables

- `ConnectionStrings__DefaultConnection`: SQLite database path
- `Jwt__Key`: JWT signing key
- `Immich__Url`: Immich server URL
- `Immich__ApiKey`: Immich API key

## API Endpoints

### Authentication
- `GET /api/auth/check-setup` - Check if initial setup is required
- `POST /api/auth/register` - Register first user
- `POST /api/auth/login` - Authenticate and get JWT token

### Configuration
- `GET /api/config` - Get current configuration
- `POST /api/config` - Save configuration
- `POST /api/config/test` - Test Immich connection

### Albums
- `GET /api/albums` - Get albums from Immich
- `GET /api/downloaded-albums` - Get locally downloaded albums
- `GET /api/stats` - Get statistics
- `GET /api/proxy/thumbnail/{assetId}` - Proxy thumbnail requests

### Profiles
- `POST /api/profiles` - Create resize profile
- `PUT /api/profiles/{id}` - Update profile
- `DELETE /api/profiles/{id}` - Delete profile

### Tasks
- `POST /api/download` - Start album download
- `POST /api/resize` - Start resize task
- `GET /api/tasks` - Get active tasks
- `GET /api/downloads` - Get completed downloads
- `GET /api/downloads/{id}` - Download ZIP file
- `DELETE /api/downloads/{id}` - Delete download

### Real-time Updates
- `WS /progressHub` - SignalR hub for progress updates

## Architecture

- **Clean Architecture**: Separation of concerns with services, controllers, and data layers
- **Dependency Injection**: Built-in DI container for service management
- **Background Services**: IHostedService for long-running tasks
- **Real-time Communication**: SignalR for WebSocket functionality
- **Image Processing**: ImageSharp for cross-platform image manipulation
- **Database**: Entity Framework Core with SQLite

## Performance Improvements

- **Native Async**: True async/await throughout the stack
- **Connection Pooling**: HttpClient factory for efficient API calls
- **Memory Management**: Proper disposal patterns and streaming
- **Compiled Code**: .NET 9 JIT compilation for optimal performance
- **Concurrent Processing**: Parallel image processing capabilities

## Migration from Python

The .NET implementation maintains full API compatibility with the Python version. Simply point your frontend to the new .NET backend URL and it will work seamlessly.

## License

Same license as the original Python implementation.