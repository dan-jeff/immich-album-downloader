# Testing Infrastructure

This document explains how to run component tests locally and on GitHub CI/CD without requiring a real Immich server.

## Component Test Architecture

The component testing infrastructure includes:

1. **TestContainers**: Real PostgreSQL database containers for integration testing
2. **WireMock.Net**: Mock Immich server that simulates realistic API responses
3. **WebApplicationFactory**: Full application test harness with dependency injection
4. **GitHub Actions**: Automated CI/CD pipeline with multiple test stages

## Running Tests Locally

### Prerequisites

- Docker installed and running
- .NET 9 SDK
- Node.js 18+ (for frontend tests)

### Backend Component Tests

```bash
# Navigate to backend directory
cd backend

# Restore dependencies
dotnet restore

# Run component tests (requires Docker)
dotnet test ImmichDownloader.Tests --filter "Category=ComponentTest" --logger "console;verbosity=detailed"

# Run unit tests only (no Docker required)
dotnet test ImmichDownloader.Tests --filter "Category!=ComponentTest" --logger "console;verbosity=detailed"

# Run all tests
dotnet test ImmichDownloader.Tests
```

### Frontend Tests

```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Run unit tests
npm test

# Run tests with coverage
npm test -- --coverage --watchAll=false
```

## GitHub CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/test.yml`) includes:

### 1. Unit Tests
- Runs .NET unit tests without external dependencies
- Uses test filter: `Category!=ComponentTest`
- Generates code coverage reports

### 2. Component Tests  
- Starts PostgreSQL service container
- Runs component tests with real database and mock Immich server
- Uses test filter: `Category=ComponentTest`

### 3. Frontend Tests
- Node.js unit tests with Jest/React Testing Library
- Coverage reporting

### 4. E2E Tests
- Full application stack with Docker Compose
- Playwright browser automation
- Real user scenarios

### 5. Security Scanning
- .NET security analysis
- npm audit for frontend dependencies
- CodeQL static analysis

### 6. Docker Build Tests
- Validates Docker images build correctly
- Tests Docker Compose configuration
- Container smoke tests

## Component Test Components

### MockImmichServer
Located: `backend/ImmichDownloader.Tests/Infrastructure/MockImmichServer.cs`

Provides realistic Immich API simulation:
- `/api/albums` - Returns test album data
- `/api/albums/{id}` - Returns album details with assets
- `/api/assets/{id}/thumbnail` - Returns mock thumbnail data
- `/api/assets/{id}` - Returns mock image data

Simulation modes:
- Default: 3 test albums with various asset counts
- Error scenarios: 500 server errors, authentication failures
- Performance testing: Large datasets (configurable album count)
- Empty server: No albums for edge case testing

### TestApplicationFactory
Located: `backend/ImmichDownloader.Tests/Infrastructure/TestApplicationFactory.cs`

Full application test environment:
- Real PostgreSQL container via TestContainers
- Mock Immich server integration
- Test user seeding
- Database cleanup between tests
- Proper dependency injection scoping

### Component Test Examples
Located: `backend/ImmichDownloader.Tests/ComponentTests/AlbumsApiComponentTests.cs`

Tests cover:
- Authentication flow with real JWT tokens
- Albums API with mock Immich integration
- Database synchronization verification
- Error handling scenarios
- Performance characteristics
- Security edge cases

## Mock Server Scenarios

The mock Immich server supports various test scenarios:

```csharp
// Default configuration - 3 test albums
_factory.ConfigureMockImmichServer(server => { /* Default */ });

// Simulate server errors
_factory.ConfigureMockImmichServer(server => server.SimulateServerErrors());

// Simulate authentication errors
_factory.ConfigureMockImmichServer(server => server.SimulateAuthenticationErrors());

// Simulate empty server
_factory.ConfigureMockImmichServer(server => server.SimulateEmptyServer());

// Simulate large dataset for performance testing
_factory.ConfigureMockImmichServer(server => server.SimulateLargeDataset(500));
```

## Database Testing

Component tests use real PostgreSQL containers:
- Isolated test database per test run
- Automatic cleanup between tests
- Real Entity Framework Core behavior
- Database migration testing

## CI/CD Configuration

### Local Development
Tests run the same way locally as in CI:
```bash
# Same commands work both locally and in CI
dotnet test --filter "Category=ComponentTest"
```

### GitHub Actions Variables
Required environment variables in CI:
- `CONNECTION_STRING` - Set automatically by PostgreSQL service
- Mock Immich server URL - Generated dynamically by WireMock

### Service Dependencies
GitHub Actions uses service containers:
```yaml
services:
  postgres:
    image: postgres:15-alpine
    env:
      POSTGRES_DB: immich_downloader_test
      POSTGRES_USER: test_user
      POSTGRES_PASSWORD: test_password
```

## Test Data and Fixtures

### Default Test Albums
The mock server provides 3 default albums:
1. **Test Album 1**: 25 assets, representative data
2. **Test Album 2**: 50 assets, different user scenarios  
3. **Empty Album**: 0 assets, edge case testing

### Test Users
Default test user credentials:
- Username: `testuser`
- Password: `TestPassword123!`

### Mock Asset Data
- Thumbnail: Valid JPEG header + minimal data
- Original images: 50KB mock JPEG files
- Realistic metadata and EXIF information

## Performance Considerations

### Test Execution Speed
- Unit tests: ~10-30 seconds
- Component tests: ~2-5 minutes (includes container startup)
- E2E tests: ~10-15 minutes (full application stack)

### Resource Usage
- PostgreSQL container: ~100MB memory
- Mock server: ~10MB memory  
- Test application: ~200MB memory

### Parallel Execution
Tests are designed for parallel execution:
- Isolated database per test class
- Port allocation for mock servers
- No shared global state

## Troubleshooting

### Common Issues

1. **Docker not running**: Component tests require Docker
   ```bash
   docker --version
   sudo systemctl start docker  # Linux
   ```

2. **Port conflicts**: Tests use dynamic port allocation
   ```bash
   # Check for port conflicts
   netstat -tulpn | grep :5432
   ```

3. **Database connection issues**: 
   ```bash
   # Verify PostgreSQL container starts
   docker logs <container_id>
   ```

4. **Test isolation issues**:
   ```bash
   # Run tests sequentially if needed
   dotnet test --parallel none
   ```

### Debug Mode

Run tests with detailed logging:
```bash
dotnet test --logger "console;verbosity=diagnostic"
```

### Container Logs

View container logs during test execution:
```bash
# List running containers during tests
docker ps

# View logs
docker logs <container_name>
```

## Future Enhancements

Planned improvements:
1. Additional controller component tests
2. Background service integration tests
3. WebSocket/SignalR testing infrastructure
4. Performance benchmarking integration
5. Database migration testing
6. Multi-database provider testing (SQLite, PostgreSQL)

## Contributing

When adding new component tests:
1. Use the `[Trait("Category", "ComponentTest")]` attribute
2. Inherit from or use `TestApplicationFactory`
3. Configure mock scenarios appropriately
4. Clean up test data in disposal methods
5. Add realistic test data and scenarios
6. Document any new mock server configurations