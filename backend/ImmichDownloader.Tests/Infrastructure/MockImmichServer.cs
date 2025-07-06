using System.Net;
using System.Text;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace ImmichDownloader.Tests.Infrastructure;

/// <summary>
/// Mock Immich server that simulates real Immich API responses for testing.
/// Provides realistic album data, thumbnail responses, and error scenarios.
/// </summary>
public class MockImmichServer : IDisposable
{
    private readonly WireMockServer _server;
    private bool _disposed;

    public string BaseUrl => _server.Url ?? throw new InvalidOperationException("Server not started");
    public int Port => _server.Port;

    public MockImmichServer()
    {
        _server = WireMockServer.Start(new WireMockServerSettings
        {
            StartAdminInterface = false,
            ReadStaticMappings = false,
            AllowPartialMapping = false
        });

        SetupDefaultMappings();
    }

    /// <summary>
    /// Sets up default API mappings that simulate a typical Immich server.
    /// </summary>
    private void SetupDefaultMappings()
    {
        // GET /api/server/ping - Server ping endpoint for validation
        _server
            .Given(Request.Create()
                .WithPath("/api/server/ping")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"res\":\"pong\"}"));

        // GET /api/albums - Returns list of albums
        _server
            .Given(Request.Create()
                .WithPath("/api/albums")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetDefaultAlbumsResponse()));

        // GET /api/albums/album-001 - Returns Test Album 1
        _server
            .Given(Request.Create()
                .WithPath("/api/albums/album-001")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetAlbumInfoResponse("album-001")));

        // GET /api/albums/album-002 - Returns Test Album 2
        _server
            .Given(Request.Create()
                .WithPath("/api/albums/album-002")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetAlbumInfoResponse("album-002")));

        // GET /api/albums/* - Returns Empty Album for any other ID
        _server
            .Given(Request.Create()
                .WithPath("/api/albums/*")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .AtPriority(10) // Lower priority than specific mappings
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetAlbumInfoResponse("unknown")));

        // GET /api/assets/{id}/thumbnail - Returns thumbnail data
        _server
            .Given(Request.Create()
                .WithPath("/api/assets/*/thumbnail")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "image/jpeg")
                .WithBody(GetMockThumbnailData()));

        // GET /api/assets/{id}/original - Returns original asset data
        _server
            .Given(Request.Create()
                .WithPath("/api/assets/*")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "image/jpeg")
                .WithHeader("Content-Disposition", "attachment; filename=\"test-image.jpg\"")
                .WithBody(GetMockImageData()));

        // Unauthorized requests (missing or invalid API key)
        _server
            .Given(Request.Create()
                .WithPath("/api/*")
                .UsingAnyMethod())
            .AtPriority(999) // Lower priority than specific mappings
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"Unauthorized\",\"error\":\"Unauthorized\",\"statusCode\":401}"));
    }

    /// <summary>
    /// Simulates an Immich server that returns errors for testing error handling.
    /// </summary>
    public void SimulateServerErrors()
    {
        // Remove existing mappings
        _server.Reset();

        // All requests return 500 Internal Server Error
        _server
            .Given(Request.Create()
                .WithPath("/api/*")
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.InternalServerError)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"Internal server error\",\"error\":\"InternalServerError\",\"statusCode\":500}"));
    }

    /// <summary>
    /// Simulates an Immich server that is extremely slow for testing timeouts.
    /// </summary>
    public void SimulateSlowServer(int delayMs = 10000)
    {
        _server.Reset();

        _server
            .Given(Request.Create()
                .WithPath("/api/*")
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithDelay(TimeSpan.FromMilliseconds(delayMs))
                .WithBody("{}"));
    }

    /// <summary>
    /// Simulates an Immich server with authentication issues.
    /// </summary>
    public void SimulateAuthenticationErrors()
    {
        _server.Reset();

        _server
            .Given(Request.Create()
                .WithPath("/api/*")
                .UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.Unauthorized)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"message\":\"Invalid API key\",\"error\":\"Unauthorized\",\"statusCode\":401}"));
    }

    /// <summary>
    /// Simulates an Immich server that returns empty album list.
    /// </summary>
    public void SimulateEmptyServer()
    {
        _server.Reset();

        _server
            .Given(Request.Create()
                .WithPath("/api/albums")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));
    }

    /// <summary>
    /// Simulates an Immich server with a large number of albums for performance testing.
    /// </summary>
    public void SimulateLargeDataset(int albumCount = 1000)
    {
        _server.Reset();

        var largeAlbumsResponse = GenerateLargeAlbumsResponse(albumCount);

        _server
            .Given(Request.Create()
                .WithPath("/api/albums")
                .WithHeader("x-api-key", "*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(largeAlbumsResponse));
    }

    /// <summary>
    /// Returns the default albums response JSON that simulates a typical Immich installation.
    /// </summary>
    private static string GetDefaultAlbumsResponse()
    {
        return """
        [
          {
            "id": "album-001",
            "albumName": "Test Album 1",
            "description": "Test album for component testing",
            "albumThumbnailAssetId": "asset-thumb-001",
            "createdAt": "2024-01-01T10:00:00.000Z",
            "updatedAt": "2024-01-01T10:00:00.000Z",
            "ownerId": "user-001",
            "owner": {
              "id": "user-001",
              "email": "test@example.com",
              "name": "Test User"
            },
            "albumUsers": [],
            "shared": false,
            "hasSharedLink": false,
            "startDate": "2024-01-01T00:00:00.000Z",
            "endDate": "2024-01-01T23:59:59.000Z",
            "assets": [],
            "assetCount": 25,
            "isActivityEnabled": true,
            "order": {},
            "lastModifiedAssetTimestamp": "2024-01-01T15:30:00.000Z"
          },
          {
            "id": "album-002",
            "albumName": "Test Album 2",
            "description": "Another test album",
            "albumThumbnailAssetId": "asset-thumb-002",
            "createdAt": "2024-01-02T10:00:00.000Z",
            "updatedAt": "2024-01-02T10:00:00.000Z",
            "ownerId": "user-001",
            "owner": {
              "id": "user-001",
              "email": "test@example.com",
              "name": "Test User"
            },
            "albumUsers": [],
            "shared": false,
            "hasSharedLink": false,
            "startDate": "2024-01-02T00:00:00.000Z",
            "endDate": "2024-01-02T23:59:59.000Z",
            "assets": [],
            "assetCount": 50,
            "isActivityEnabled": true,
            "order": {},
            "lastModifiedAssetTimestamp": "2024-01-02T18:45:00.000Z"
          },
          {
            "id": "album-003",
            "albumName": "Empty Album",
            "description": "Album with no assets",
            "albumThumbnailAssetId": null,
            "createdAt": "2024-01-03T10:00:00.000Z",
            "updatedAt": "2024-01-03T10:00:00.000Z",
            "ownerId": "user-001",
            "owner": {
              "id": "user-001",
              "email": "test@example.com",
              "name": "Test User"
            },
            "albumUsers": [],
            "shared": false,
            "hasSharedLink": false,
            "startDate": null,
            "endDate": null,
            "assets": [],
            "assetCount": 0,
            "isActivityEnabled": false,
            "order": {},
            "lastModifiedAssetTimestamp": null
          }
        ]
        """;
    }

    /// <summary>
    /// Returns album info with assets for a specific album ID.
    /// </summary>
    private static string GetAlbumInfoResponse(string albumId)
    {
        return albumId switch
        {
            "album-001" => """
            {
              "id": "album-001",
              "albumName": "Test Album 1",
              "description": "Test album for component testing",
              "albumThumbnailAssetId": "asset-thumb-001",
              "createdAt": "2024-01-01T10:00:00.000Z",
              "updatedAt": "2024-01-01T10:00:00.000Z",
              "ownerId": "user-001",
              "owner": {
                "id": "user-001",
                "email": "test@example.com",
                "name": "Test User"
              },
              "albumUsers": [],
              "shared": false,
              "hasSharedLink": false,
              "startDate": "2024-01-01T00:00:00.000Z",
              "endDate": "2024-01-01T23:59:59.000Z",
              "assets": [
                {
                  "id": "asset-001",
                  "type": "IMAGE",
                  "originalFileName": "test-image-1.jpg",
                  "fileCreatedAt": "2024-01-01T12:00:00.000Z",
                  "fileModifiedAt": "2024-01-01T12:00:00.000Z",
                  "isFavorite": false,
                  "isArchived": false,
                  "exifInfo": {
                    "make": "Test Camera",
                    "model": "Test Model",
                    "fileSizeInByte": 1024000
                  }
                },
                {
                  "id": "asset-002",
                  "type": "IMAGE",
                  "originalFileName": "test-image-2.jpg",
                  "fileCreatedAt": "2024-01-01T13:00:00.000Z",
                  "fileModifiedAt": "2024-01-01T13:00:00.000Z",
                  "isFavorite": false,
                  "isArchived": false,
                  "exifInfo": {
                    "make": "Test Camera",
                    "model": "Test Model",
                    "fileSizeInByte": 2048000
                  }
                }
              ],
              "assetCount": 25,
              "isActivityEnabled": true,
              "order": {},
              "lastModifiedAssetTimestamp": "2024-01-01T15:30:00.000Z"
            }
            """,
            "album-002" => """
            {
              "id": "album-002",
              "albumName": "Test Album 2",
              "description": "Another test album",
              "albumThumbnailAssetId": "asset-thumb-002",
              "createdAt": "2024-01-02T10:00:00.000Z",
              "updatedAt": "2024-01-02T10:00:00.000Z",
              "ownerId": "user-001",
              "owner": {
                "id": "user-001",
                "email": "test@example.com",
                "name": "Test User"
              },
              "albumUsers": [],
              "shared": false,
              "hasSharedLink": false,
              "startDate": "2024-01-02T00:00:00.000Z",
              "endDate": "2024-01-02T23:59:59.000Z",
              "assets": [
                {
                  "id": "asset-003",
                  "type": "IMAGE",
                  "originalFileName": "test-image-3.jpg",
                  "fileCreatedAt": "2024-01-02T14:00:00.000Z",
                  "fileModifiedAt": "2024-01-02T14:00:00.000Z",
                  "isFavorite": true,
                  "isArchived": false,
                  "exifInfo": {
                    "make": "Test Camera",
                    "model": "Test Model",
                    "fileSizeInByte": 3072000
                  }
                }
              ],
              "assetCount": 50,
              "isActivityEnabled": true,
              "order": {},
              "lastModifiedAssetTimestamp": "2024-01-02T18:45:00.000Z"
            }
            """,
            _ => """
            {
              "id": "album-003",
              "albumName": "Empty Album",
              "description": "Album with no assets",
              "albumThumbnailAssetId": null,
              "createdAt": "2024-01-03T10:00:00.000Z",
              "updatedAt": "2024-01-03T10:00:00.000Z",
              "ownerId": "user-001",
              "owner": {
                "id": "user-001",
                "email": "test@example.com",
                "name": "Test User"
              },
              "albumUsers": [],
              "shared": false,
              "hasSharedLink": false,
              "startDate": null,
              "endDate": null,
              "assets": [],
              "assetCount": 0,
              "isActivityEnabled": false,
              "order": {},
              "lastModifiedAssetTimestamp": null
            }
            """
        };
    }

    /// <summary>
    /// Generates a large albums response for performance testing.
    /// </summary>
    private static string GenerateLargeAlbumsResponse(int count)
    {
        var albums = new StringBuilder("[");
        
        for (int i = 1; i <= count; i++)
        {
            if (i > 1) albums.Append(",");
            
            albums.Append($$$"""
            {
              "id": "album-{{{i:D6}}}",
              "albumName": "Test Album {{{i}}}",
              "description": "Generated test album {{{i}}}",
              "albumThumbnailAssetId": "asset-thumb-{{{i:D6}}}",
              "createdAt": "2024-01-01T10:00:00.000Z",
              "updatedAt": "2024-01-01T10:00:00.000Z",
              "ownerId": "user-001",
              "owner": {
                "id": "user-001",
                "email": "test@example.com",
                "name": "Test User"
              },
              "albumUsers": [],
              "shared": false,
              "hasSharedLink": false,
              "startDate": "2024-01-01T00:00:00.000Z",
              "endDate": "2024-01-01T23:59:59.000Z",
              "assets": [],
              "assetCount": {{{i % 100 + 1}}},
              "isActivityEnabled": true,
              "order": {},
              "lastModifiedAssetTimestamp": "2024-01-01T15:30:00.000Z"
            }
            """);
        }
        
        albums.Append("]");
        return albums.ToString();
    }

    /// <summary>
    /// Returns mock thumbnail data (minimal JPEG header).
    /// </summary>
    private static byte[] GetMockThumbnailData()
    {
        // Minimal valid JPEG header + data
        return new byte[] 
        { 
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x01, 0x00, 0x48,
            0x00, 0x48, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08,
            0x07, 0x07, 0x07, 0x09, 0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
            0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20, 0x24, 0x2E, 0x27, 0x20,
            0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29, 0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27,
            0x39, 0x3D, 0x38, 0x32, 0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xD9
        };
    }

    /// <summary>
    /// Returns mock image data (minimal JPEG with larger size).
    /// </summary>
    private static byte[] GetMockImageData()
    {
        // Create a larger mock JPEG for original image downloads
        var data = new byte[50000]; // 50KB mock image
        
        // JPEG header
        data[0] = 0xFF;
        data[1] = 0xD8;
        data[2] = 0xFF;
        data[3] = 0xE0;
        
        // Fill with some mock data
        for (int i = 4; i < data.Length - 2; i++)
        {
            data[i] = (byte)(i % 256);
        }
        
        // JPEG footer
        data[^2] = 0xFF;
        data[^1] = 0xD9;
        
        return data;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _server?.Stop();
            _server?.Dispose();
            _disposed = true;
        }
    }
}