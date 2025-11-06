# Immich Album Downloader - API Documentation

## Table of Contents

- [Overview](#overview)
- [Authentication](#authentication)
- [API Endpoints](#api-endpoints)
  - [Auth Endpoints](#auth-endpoints)
  - [Configuration Endpoints](#configuration-endpoints)
  - [Album Endpoints](#album-endpoints)
  - [Task Endpoints](#task-endpoints)
  - [Download Endpoints](#download-endpoints)
  - [Profile Endpoints](#profile-endpoints)
  - [Health Endpoints](#health-endpoints)
- [Data Models](#data-models)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)
- [Versioning](#versioning)

---

## Overview

The Immich Album Downloader API is a RESTful API built with ASP.NET Core 9.0. It provides endpoints for downloading and processing photos from Immich servers with real-time progress tracking via SignalR.

**Base URL**: `http://localhost:8082/api` (development)

**Content Type**: `application/json`

**Authentication**: JWT Bearer Token

---

## Authentication

All endpoints except `/auth/check-setup`, `/auth/register`, and `/auth/login` require authentication.

### Bearer Token Format

```http
Authorization: Bearer <jwt_token>
```

### Token Characteristics

- **Algorithm**: HS256 (HMAC-SHA256)
- **Lifetime**: Configurable (default: 24 hours)
- **Claims**:
  - `sub`: User ID
  - `nameid`: User ID
  - `unique_name`: Username
  - `jti`: Unique token ID
  - `iat`: Issued at timestamp
  - `exp`: Expiration timestamp
  - `iss`: Issuer (ImmichDownloader)
  - `aud`: Audience (ImmichDownloader)

---

## API Endpoints

### Auth Endpoints

#### `GET /api/auth/check-setup`

Check if the application requires initial setup.

**Authentication**: None required

**Response**:
```json
{
  "setup_required": true
}
```

**Status Codes**:
- `200 OK`: Setup status retrieved successfully

---

#### `POST /api/auth/register`

Register the first user account. Only works if no users exist.

**Authentication**: None required

**Request Body**:
```json
{
  "username": "admin",
  "password": "SecurePassword123!"
}
```

**Validation Rules**:
- Username: 3-50 characters, no whitespace
- Password: 8-128 characters

**Response**:
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer"
}
```

**Status Codes**:
- `200 OK`: User created and authenticated successfully
- `400 Bad Request`: Invalid input or user already exists
- `422 Unprocessable Entity`: Validation errors

**Example**:
```bash
curl -X POST http://localhost:8082/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"SecurePassword123!"}'
```

---

#### `POST /api/auth/login`

Authenticate a user and receive a JWT token.

**Authentication**: None required

**Request Body**:
```json
{
  "username": "admin",
  "password": "SecurePassword123!"
}
```

**Response**:
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer"
}
```

**Status Codes**:
- `200 OK`: Authentication successful
- `400 Bad Request`: Invalid request format
- `401 Unauthorized`: Invalid credentials

**Example**:
```bash
curl -X POST http://localhost:8082/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"SecurePassword123!"}'
```

---

### Configuration Endpoints

#### `GET /api/config`

Retrieve application configuration including Immich server settings.

**Authentication**: Required

**Response**:
```json
{
  "immich_url": "https://immich.example.com",
  "api_key": "immich_api_key_here",
  "resize_profiles": [
    {
      "id": 1,
      "name": "Photo Frame 1920x1080",
      "width": 1920,
      "height": 1080,
      "include_horizontal": true,
      "include_vertical": false,
      "quality": 85,
      "created_at": "2024-01-15T10:00:00Z"
    }
  ]
}
```

**Status Codes**:
- `200 OK`: Configuration retrieved successfully
- `401 Unauthorized`: Missing or invalid token

---

#### `POST /api/config`

Save Immich server configuration.

**Authentication**: Required

**Request Body**:
```json
{
  "immich_url": "https://immich.example.com",
  "api_key": "your_immich_api_key"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Configuration saved successfully"
}
```

**Status Codes**:
- `200 OK`: Configuration saved successfully
- `400 Bad Request`: Invalid input
- `401 Unauthorized`: Missing or invalid token

---

#### `POST /api/config/test`

Test connection to Immich server with provided credentials.

**Authentication**: Required

**Request Body**:
```json
{
  "immich_url": "https://immich.example.com",
  "api_key": "your_immich_api_key"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Connection successful"
}
```

**Status Codes**:
- `200 OK`: Connection test completed (check `success` field for result)
- `400 Bad Request`: Invalid input
- `401 Unauthorized`: Missing or invalid token

**Example**:
```bash
curl -X POST http://localhost:8082/api/config/test \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"immich_url":"https://immich.example.com","api_key":"key"}'
```

---

### Album Endpoints

#### `GET /api/albums`

Retrieve all albums from Immich server with local asset counts.

**Authentication**: Required

**Response**:
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "albumName": "Family Photos 2024",
    "description": "Summer vacation photos",
    "albumThumbnailAssetId": "asset-id-123",
    "assetCount": 156,
    "localAssetCount": 100,
    "shared": false,
    "hasSharedLink": false,
    "isActivityEnabled": false,
    "createdAt": "2024-01-15T10:00:00Z",
    "updatedAt": "2024-01-20T15:30:00Z"
  }
]
```

**Field Descriptions**:
- `id`: Immich album UUID
- `albumName`: Album name from Immich
- `assetCount`: Total images in Immich album
- `localAssetCount`: Number of images downloaded locally
- `albumThumbnailAssetId`: Asset ID for album cover image

**Status Codes**:
- `200 OK`: Albums retrieved successfully
- `400 Bad Request`: Immich not configured
- `401 Unauthorized`: Missing or invalid token
- `500 Internal Server Error`: Immich server communication error

**Example**:
```bash
curl -X GET http://localhost:8082/api/albums \
  -H "Authorization: Bearer <token>"
```

---

#### `GET /api/downloaded-albums`

Retrieve all albums that have been downloaded locally.

**Authentication**: Required

**Response**:
```json
[
  {
    "id": 1,
    "albumName": "Family Photos 2024",
    "assetCount": 0,
    "localAssetCount": 156,
    "shared": false,
    "sharedUsers": []
  }
]
```

**Status Codes**:
- `200 OK`: Downloaded albums retrieved successfully
- `401 Unauthorized`: Missing or invalid token

---

#### `DELETE /api/albums/{albumId}/local`

Remove locally downloaded assets for a specific album.

**Authentication**: Required

**Path Parameters**:
- `albumId` (string): The Immich album UUID

**Response**:
```json
{
  "success": true,
  "message": "Removed 156 local assets for album"
}
```

**Status Codes**:
- `200 OK`: Local assets removed successfully
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Album not found

---

#### `POST /api/albums/cleanup-orphans`

Clean up orphaned assets (assets not in any album).

**Authentication**: Required

**Response**:
```json
{
  "success": true,
  "message": "Cleaned up 23 orphaned assets",
  "summary": {
    "albumsProcessed": 5,
    "orphansFound": 23,
    "orphansRemoved": 23
  },
  "details": [
    {
      "albumId": "album-id-1",
      "albumName": "Album 1",
      "orphansFound": 10,
      "orphansRemoved": 10
    }
  ]
}
```

**Status Codes**:
- `200 OK`: Cleanup completed successfully
- `401 Unauthorized`: Missing or invalid token

---

#### `GET /api/proxy/thumbnail/{assetId}`

Proxy thumbnail requests to Immich server.

**Authentication**: Required

**Path Parameters**:
- `assetId` (string): The Immich asset UUID

**Response**: Binary image data (JPEG/PNG)

**Status Codes**:
- `200 OK`: Thumbnail retrieved successfully
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Asset not found
- `500 Internal Server Error`: Immich server error

---

### Task Endpoints

#### `POST /api/download`

Start a new album download task.

**Authentication**: Required

**Request Body**:
```json
{
  "AlbumId": "550e8400-e29b-41d4-a716-446655440000",
  "AlbumName": "Family Photos 2024"
}
```

**Response**:
```json
{
  "task_id": "download-task-uuid-123"
}
```

**Status Codes**:
- `200 OK`: Download task started successfully
- `400 Bad Request`: Invalid input
- `401 Unauthorized`: Missing or invalid token
- `503 Service Unavailable`: Task queue full (max 100 tasks)

**Example**:
```bash
curl -X POST http://localhost:8082/api/download \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"AlbumId":"550e8400-...","AlbumName":"Family Photos"}'
```

**Progress Tracking**: Use SignalR connection to `/progressHub` for real-time updates

---

#### `POST /api/resize`

Start a new image resize task.

**Authentication**: Required

**Request Body**:
```json
{
  "DownloadedAlbumId": 1,
  "ProfileId": 2
}
```

**Response**:
```json
{
  "task_id": "resize-task-uuid-456"
}
```

**Status Codes**:
- `200 OK`: Resize task started successfully
- `400 Bad Request`: Invalid input or album/profile not found
- `401 Unauthorized`: Missing or invalid token
- `503 Service Unavailable`: Task queue full

---

#### `GET /api/tasks`

Retrieve all background tasks (downloads and resizes).

**Authentication**: Required

**Response**:
```json
[
  {
    "id": "task-uuid-123",
    "taskType": "Download",
    "status": "InProgress",
    "progress": 75,
    "total": 100,
    "currentStep": "Downloaded 75/100 photos",
    "albumId": "550e8400-e29b-41d4-a716-446655440000",
    "albumName": "Family Photos 2024",
    "createdAt": "2024-01-15T10:30:00Z",
    "completedAt": null
  },
  {
    "id": "task-uuid-456",
    "taskType": "Resize",
    "status": "Completed",
    "progress": 50,
    "total": 50,
    "currentStep": "Resize complete!",
    "albumId": null,
    "albumName": "Family Photos 2024",
    "profileId": 2,
    "createdAt": "2024-01-15T11:00:00Z",
    "completedAt": "2024-01-15T11:05:23Z"
  }
]
```

**Field Descriptions**:
- `taskType`: "Download" or "Resize"
- `status`: "Pending", "InProgress", "Completed", "Error"
- `progress`: Current progress count
- `total`: Total items to process
- `currentStep`: Human-readable status message

**Status Codes**:
- `200 OK`: Tasks retrieved successfully
- `401 Unauthorized`: Missing or invalid token

---

#### `DELETE /api/tasks/{taskId}`

Delete a completed or failed task.

**Authentication**: Required

**Path Parameters**:
- `taskId` (string): The task UUID

**Response**:
```json
{
  "success": true,
  "message": "Task deleted successfully"
}
```

**Status Codes**:
- `200 OK`: Task deleted successfully
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Task not found

---

### Download Endpoints

#### `GET /api/downloads`

Retrieve all completed downloads available for download.

**Authentication**: Required

**Response**:
```json
[
  {
    "id": "task-uuid-123",
    "taskType": "Download",
    "albumName": "Family Photos 2024",
    "fileSize": 524288000,
    "completedAt": "2024-01-15T10:45:00Z",
    "filename": "Family_Photos_2024.zip"
  },
  {
    "id": "task-uuid-456",
    "taskType": "Resize",
    "albumName": "Family Photos 2024 - Photo Frame 1920x1080",
    "profileName": "Photo Frame 1920x1080",
    "fileSize": 314572800,
    "completedAt": "2024-01-15T11:05:23Z",
    "filename": "Family_Photos_2024_PhotoFrame1920x1080.zip"
  }
]
```

**Status Codes**:
- `200 OK`: Downloads retrieved successfully
- `401 Unauthorized`: Missing or invalid token

---

#### `GET /api/downloads/{taskId}`

Download a completed ZIP file.

**Authentication**: Required

**Path Parameters**:
- `taskId` (string): The task UUID

**Response**: Binary ZIP file stream

**Headers**:
- `Content-Type`: `application/zip`
- `Content-Disposition`: `attachment; filename="AlbumName.zip"`
- `Accept-Ranges`: `bytes` (supports resume)

**Status Codes**:
- `200 OK`: File download started
- `206 Partial Content`: Range request served
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: File not found
- `416 Range Not Satisfiable`: Invalid range

**Example**:
```bash
# Download entire file
curl -X GET http://localhost:8082/api/downloads/task-uuid-123 \
  -H "Authorization: Bearer <token>" \
  -o album.zip

# Resume download from byte 1000000
curl -X GET http://localhost:8082/api/downloads/task-uuid-123 \
  -H "Authorization: Bearer <token>" \
  -H "Range: bytes=1000000-" \
  -o album.zip
```

---

#### `DELETE /api/downloads/{taskId}`

Delete a completed download and its associated file.

**Authentication**: Required

**Path Parameters**:
- `taskId` (string): The task UUID

**Response**:
```json
{
  "success": true,
  "message": "Download deleted successfully"
}
```

**Status Codes**:
- `200 OK`: Download deleted successfully
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Download not found

---

### Profile Endpoints

#### `GET /api/profiles`

Retrieve all resize profiles.

**Authentication**: Required

**Response**:
```json
[
  {
    "id": 1,
    "name": "Photo Frame 1920x1080",
    "width": 1920,
    "height": 1080,
    "include_horizontal": true,
    "include_vertical": false,
    "quality": 85,
    "created_at": "2024-01-15T10:00:00Z"
  }
]
```

**Status Codes**:
- `200 OK`: Profiles retrieved successfully
- `401 Unauthorized`: Missing or invalid token

---

#### `POST /api/profiles`

Create a new resize profile.

**Authentication**: Required

**Request Body**:
```json
{
  "Name": "Photo Frame 1920x1080",
  "Width": 1920,
  "Height": 1080,
  "IncludeHorizontal": true,
  "IncludeVertical": false,
  "Quality": 85
}
```

**Validation Rules**:
- Name: 1-100 characters, required
- Width: 100-10000 pixels, required
- Height: 100-10000 pixels, required
- Quality: 1-100 (JPEG quality percentage), default: 85

**Response**:
```json
{
  "success": true,
  "id": 2,
  "message": "Profile created successfully"
}
```

**Status Codes**:
- `200 OK`: Profile created successfully
- `400 Bad Request`: Invalid input
- `401 Unauthorized`: Missing or invalid token

---

#### `PUT /api/profiles/{profileId}`

Update an existing resize profile.

**Authentication**: Required

**Path Parameters**:
- `profileId` (integer): The profile ID

**Request Body**:
```json
{
  "Name": "Photo Frame 1920x1080 High Quality",
  "Width": 1920,
  "Height": 1080,
  "IncludeHorizontal": true,
  "IncludeVertical": true,
  "Quality": 95
}
```

**Response**:
```json
{
  "success": true,
  "message": "Profile updated successfully"
}
```

**Status Codes**:
- `200 OK`: Profile updated successfully
- `400 Bad Request`: Invalid input
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Profile not found

---

#### `DELETE /api/profiles/{profileId}`

Delete a resize profile.

**Authentication**: Required

**Path Parameters**:
- `profileId` (integer): The profile ID

**Response**:
```json
{
  "success": true,
  "message": "Profile deleted successfully"
}
```

**Status Codes**:
- `200 OK`: Profile deleted successfully
- `401 Unauthorized`: Missing or invalid token
- `404 Not Found`: Profile not found
- `409 Conflict`: Profile is in use by active tasks

---

### Health Endpoints

#### `GET /api/health`

Check API health status.

**Authentication**: None required

**Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T12:00:00Z",
  "version": "1.0.0",
  "environment": "Production"
}
```

**Status Codes**:
- `200 OK`: Service is healthy
- `503 Service Unavailable`: Service is unhealthy

---

#### `GET /api/stats`

Get application statistics.

**Authentication**: Required

**Response**:
```json
{
  "album_count": 25,
  "image_count": 3450,
  "download_count": 15
}
```

**Status Codes**:
- `200 OK`: Statistics retrieved successfully
- `401 Unauthorized`: Missing or invalid token

---

## Data Models

### User

```json
{
  "id": 1,
  "username": "admin",
  "created_at": "2024-01-15T10:00:00Z"
}
```

### Album

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "albumName": "Family Photos 2024",
  "description": "Summer vacation",
  "assetCount": 156,
  "localAssetCount": 100,
  "albumThumbnailAssetId": "asset-id-123",
  "shared": false,
  "createdAt": "2024-01-15T10:00:00Z",
  "updatedAt": "2024-01-20T15:30:00Z"
}
```

### ResizeProfile

```json
{
  "id": 1,
  "name": "Photo Frame 1920x1080",
  "width": 1920,
  "height": 1080,
  "include_horizontal": true,
  "include_vertical": false,
  "quality": 85,
  "created_at": "2024-01-15T10:00:00Z"
}
```

### BackgroundTask

```json
{
  "id": "task-uuid-123",
  "taskType": "Download",
  "status": "InProgress",
  "progress": 75,
  "total": 100,
  "currentStep": "Downloaded 75/100 photos",
  "albumId": "550e8400-e29b-41d4-a716-446655440000",
  "albumName": "Family Photos 2024",
  "profileId": null,
  "createdAt": "2024-01-15T10:30:00Z",
  "completedAt": null
}
```

**Enums**:
- `taskType`: "Download" | "Resize"
- `status`: "Pending" | "InProgress" | "Completed" | "Error"

---

## Error Handling

### Error Response Format

```json
{
  "error": "Error message",
  "detail": "Detailed error information",
  "status": 400,
  "timestamp": "2024-01-15T12:00:00Z"
}
```

### Common Error Codes

| Status Code | Description |
|-------------|-------------|
| `400 Bad Request` | Invalid input or malformed request |
| `401 Unauthorized` | Missing or invalid JWT token |
| `403 Forbidden` | Valid token but insufficient permissions |
| `404 Not Found` | Resource not found |
| `409 Conflict` | Resource conflict (e.g., duplicate) |
| `422 Unprocessable Entity` | Validation errors |
| `429 Too Many Requests` | Rate limit exceeded |
| `500 Internal Server Error` | Server-side error |
| `503 Service Unavailable` | Service temporarily unavailable |

### Validation Errors

```json
{
  "errors": {
    "Username": ["Username must be between 3 and 50 characters"],
    "Password": ["Password must be at least 8 characters"]
  },
  "status": 422,
  "title": "One or more validation errors occurred"
}
```

---

## Rate Limiting

**Implementation**: Token bucket algorithm

**Limits** (default):
- Authenticated endpoints: 100 requests per minute
- Authentication endpoints: 10 requests per minute
- Download endpoints: 5 concurrent downloads

**Rate Limit Headers**:
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1705320000
```

**Rate Limit Exceeded Response**:
```json
{
  "error": "Rate limit exceeded",
  "detail": "Try again in 42 seconds",
  "status": 429,
  "retryAfter": 42
}
```

---

## SignalR Real-Time Updates

### Connection

**Endpoint**: `ws://localhost:8082/progressHub`

**Authentication**: JWT token via query parameter

**Example** (JavaScript):
```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/progressHub', {
    accessTokenFactory: () => localStorage.getItem('token')
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

### Events

#### `TaskStatusUpdated`

Sent when a task's status changes.

**Payload**:
```json
{
  "TaskId": "task-uuid-123",
  "Type": "download",
  "Status": "inprogress",
  "Progress": 75,
  "Total": 100,
  "Message": "Downloaded 75/100 photos"
}
```

**Example** (JavaScript):
```javascript
connection.on('TaskStatusUpdated', (message) => {
  console.log('Task update:', message);
  // Update UI with progress
});
```

---

## Versioning

**Current Version**: v1

**Version Header** (future):
```http
Accept-Version: application/vnd.immich-downloader.v1+json
```

**Deprecation Notice**:
- Deprecated endpoints will include `X-Deprecated: true` header
- Minimum 6 months notice before removal
- Migration guide provided in documentation

---

## Examples

### Complete Download Workflow

```bash
# 1. Login
TOKEN=$(curl -X POST http://localhost:8082/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"password"}' \
  | jq -r '.access_token')

# 2. Configure Immich
curl -X POST http://localhost:8082/api/config \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"immich_url":"https://immich.example.com","api_key":"key"}'

# 3. Get albums
ALBUM_ID=$(curl -X GET http://localhost:8082/api/albums \
  -H "Authorization: Bearer $TOKEN" \
  | jq -r '.[0].id')

# 4. Start download
TASK_ID=$(curl -X POST http://localhost:8082/api/download \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"AlbumId\":\"$ALBUM_ID\",\"AlbumName\":\"My Album\"}" \
  | jq -r '.task_id')

# 5. Check progress
curl -X GET http://localhost:8082/api/tasks \
  -H "Authorization: Bearer $TOKEN" \
  | jq ".[] | select(.id==\"$TASK_ID\")"

# 6. Download ZIP
curl -X GET "http://localhost:8082/api/downloads/$TASK_ID" \
  -H "Authorization: Bearer $TOKEN" \
  -o album.zip
```

---

**Document Version**: 1.0
**Last Updated**: 2025-11-06
**API Version**: v1
**Status**: Complete
