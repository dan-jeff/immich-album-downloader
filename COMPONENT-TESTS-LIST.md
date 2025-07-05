# Component Tests Comprehensive List

## Overview
This document outlines all component tests that should be implemented for the Immich Album Downloader project. Component tests verify the integration between multiple parts of the system using real databases and mock external services.

---

## 1. Authentication Controller Component Tests
**File**: `AuthControllerComponentTests.cs`
**Infrastructure**: In-memory database, WebApplicationFactory

### Test Categories

#### 1.1 Setup and Registration Flow
- [ ] `CheckSetup_WithNoUsers_ShouldReturnNeedsSetupTrue()`
- [ ] `CheckSetup_WithExistingUsers_ShouldReturnNeedsSetupFalse()`
- [ ] `Register_FirstUser_ShouldCreateSuccessfully()`
- [ ] `Register_WhenUsersExist_ShouldReturn409Conflict()`
- [ ] `Register_WithInvalidPassword_ShouldReturn400WithValidationErrors()`
- [ ] `Register_WithDuplicateUsername_ShouldReturn400()`

#### 1.2 Login and Authentication
- [ ] `Login_WithValidCredentials_ShouldReturnJwtToken()`
- [ ] `Login_WithInvalidCredentials_ShouldReturn401()`
- [ ] `Login_WithNonExistentUser_ShouldReturn401()`
- [ ] `Login_WithEmptyCredentials_ShouldReturn400()`

#### 1.3 JWT Integration
- [ ] `AuthenticatedRequest_WithValidJwt_ShouldAllowAccess()`
- [ ] `AuthenticatedRequest_WithExpiredJwt_ShouldReturn401()`
- [ ] `AuthenticatedRequest_WithInvalidJwt_ShouldReturn401()`
- [ ] `AuthenticatedRequest_WithMissingJwt_ShouldReturn401()`

---

## 2. Albums Controller Component Tests
**File**: `AlbumsControllerComponentTests.cs`
**Infrastructure**: PostgreSQL TestContainer, Mock Immich Server

### Test Categories

#### 2.1 Album Synchronization
- [ ] `GetAlbums_WithValidConfiguration_ShouldSyncFromImmichServer()`
- [ ] `GetAlbums_WithNoConfiguration_ShouldReturn500WithMessage()`
- [ ] `GetAlbums_WithInvalidImmichCredentials_ShouldReturn500()`
- [ ] `GetAlbums_WithImmichServerDown_ShouldReturn500()`
- [ ] `GetAlbums_ShouldUpdateLocalDatabase()`
- [ ] `GetAlbums_ShouldHandleLargeAlbumCounts()`

#### 2.2 Database Integration
- [ ] `AlbumSync_ShouldCreateNewAlbums()`
- [ ] `AlbumSync_ShouldUpdateExistingAlbums()`
- [ ] `AlbumSync_ShouldRemoveDeletedAlbums()`
- [ ] `AlbumSync_ShouldHandleDuplicateAlbumIds()`

#### 2.3 Downloaded Albums
- [ ] `GetDownloadedAlbums_ShouldReturnCompletedDownloads()`
- [ ] `GetDownloadedAlbums_WithNoDownloads_ShouldReturnEmpty()`
- [ ] `GetDownloadedAlbums_ShouldIncludeFileInfo()`

#### 2.4 Statistics
- [ ] `GetStats_ShouldCalculateCorrectCounts()`
- [ ] `GetStats_WithMixedData_ShouldReturnAccurateStats()`
- [ ] `GetStats_WithEmptyDatabase_ShouldReturnZeros()`

#### 2.5 Thumbnail Proxy
- [ ] `ProxyThumbnail_WithValidAsset_ShouldReturnImageData()`
- [ ] `ProxyThumbnail_WithInvalidAsset_ShouldReturn404()`
- [ ] `ProxyThumbnail_WithImmichServerDown_ShouldReturn404()`
- [ ] `ProxyThumbnail_ShouldSetCorrectCacheHeaders()`
- [ ] `ProxyThumbnail_ShouldHandleImageFormats()`

---

## 3. Configuration Controller Component Tests
**File**: `ConfigControllerComponentTests.cs`
**Infrastructure**: PostgreSQL TestContainer, Mock Immich Server

### Test Categories

#### 3.1 Configuration Management
- [ ] `GetConfig_ShouldReturnCurrentSettings()`
- [ ] `SaveConfig_WithValidSettings_ShouldPersistToDatabase()`
- [ ] `SaveConfig_WithInvalidUrl_ShouldReturn400()`
- [ ] `SaveConfig_WithEmptyApiKey_ShouldReturn400()`

#### 3.2 Connection Testing
- [ ] `TestConnection_WithValidCredentials_ShouldReturnSuccess()`
- [ ] `TestConnection_WithInvalidCredentials_ShouldReturnError()`
- [ ] `TestConnection_WithServerDown_ShouldReturnError()`
- [ ] `TestConnection_WithMalformedUrl_ShouldReturn400()`

#### 3.3 Resize Profiles CRUD
- [ ] `CreateProfile_WithValidData_ShouldPersistToDatabase()`
- [ ] `CreateProfile_WithInvalidDimensions_ShouldReturn400()`
- [ ] `UpdateProfile_WithValidChanges_ShouldUpdateDatabase()`
- [ ] `UpdateProfile_WithNonExistentId_ShouldReturn404()`
- [ ] `DeleteProfile_WithExistingId_ShouldRemoveFromDatabase()`
- [ ] `DeleteProfile_WithNonExistentId_ShouldReturn404()`
- [ ] `DeleteProfile_WhenInUse_ShouldReturn409()`

---

## 4. Tasks Controller Component Tests
**File**: `TasksControllerComponentTests.cs`
**Infrastructure**: PostgreSQL TestContainer, Mock Immich Server, File System

### Test Categories

#### 4.1 Download Task Management
- [ ] `StartDownload_WithValidAlbum_ShouldCreateBackgroundTask()`
- [ ] `StartDownload_WithInvalidAlbumId_ShouldReturn400()`
- [ ] `StartDownload_WithImmichServerDown_ShouldReturn500()`
- [ ] `StartDownload_WithConcurrentRequests_ShouldQueueProperly()`

#### 4.2 Resize Task Management
- [ ] `StartResize_WithValidDownload_ShouldCreateBackgroundTask()`
- [ ] `StartResize_WithInvalidDownloadId_ShouldReturn400()`
- [ ] `StartResize_WithInvalidProfileId_ShouldReturn400()`
- [ ] `StartResize_WithMissingFiles_ShouldReturn400()`

#### 4.3 Task Status and Monitoring
- [ ] `GetTasks_ShouldReturnActiveBackgroundTasks()`
- [ ] `GetTasks_ShouldIncludeProgressInformation()`
- [ ] `GetTasks_ShouldFilterCompletedTasks()`

#### 4.4 Download Streaming
- [ ] `GetDownload_WithCompletedTask_ShouldStreamZipFile()`
- [ ] `GetDownload_WithInProgressTask_ShouldReturn404()`
- [ ] `GetDownload_WithNonExistentTask_ShouldReturn404()`
- [ ] `GetDownload_ShouldSetCorrectHeaders()`

#### 4.5 Cleanup Operations
- [ ] `DeleteDownload_ShouldRemoveFilesAndDatabase()`
- [ ] `DeleteDownload_ShouldHandlePartialCleanup()`
- [ ] `DeleteTask_ShouldCancelRunningTask()`
- [ ] `DeleteTask_ShouldCleanupResources()`

---

## 5. Streaming Download Service Component Tests
**File**: `StreamingDownloadServiceComponentTests.cs`
**Infrastructure**: PostgreSQL TestContainer, Mock Immich Server, Temporary File System

### Test Categories

#### 5.1 Core Download Functionality
- [ ] `StartDownloadAsync_WithValidAlbum_ShouldCreateZipFile()`
- [ ] `StartDownloadAsync_ShouldDownloadAllAssets()`
- [ ] `StartDownloadAsync_ShouldHandleEmptyAlbums()`
- [ ] `StartDownloadAsync_ShouldPreserveFolderStructure()`

#### 5.2 Progress Tracking
- [ ] `StartDownloadAsync_ShouldReportProgressUpdates()`
- [ ] `StartDownloadAsync_ShouldNotifyCompletion()`
- [ ] `StartDownloadAsync_ShouldReportErrors()`

#### 5.3 Memory Management
- [ ] `StartDownloadAsync_WithLargeAlbum_ShouldNotExceedMemoryLimits()`
- [ ] `StartDownloadAsync_ShouldProcessInChunks()`
- [ ] `StartDownloadAsync_ShouldStreamDirectlyToDisk()`

#### 5.4 Error Handling
- [ ] `StartDownloadAsync_WithNetworkErrors_ShouldRetryAndFail()`
- [ ] `StartDownloadAsync_WithDiskSpaceError_ShouldFailGracefully()`
- [ ] `StartDownloadAsync_WithCancellation_ShouldCleanupPartialFiles()`
- [ ] `StartDownloadAsync_WithImmichErrors_ShouldSkipFailedAssets()`

#### 5.5 File Management
- [ ] `GetDownloadStream_ShouldReturnValidZipStream()`
- [ ] `DeleteDownloadAsync_ShouldRemoveAllFiles()`
- [ ] `DeleteDownloadAsync_ShouldHandleFileLocks()`

---

## 6. Streaming Resize Service Component Tests
**File**: `StreamingResizeServiceComponentTests.cs`
**Infrastructure**: PostgreSQL TestContainer, Test Images, Temporary File System

### Test Categories

#### 6.1 Core Resize Functionality
- [ ] `StartResizeAsync_WithValidProfile_ShouldResizeImages()`
- [ ] `StartResizeAsync_ShouldCreateZipFile()`
- [ ] `StartResizeAsync_ShouldHandleMultipleFormats()`
- [ ] `StartResizeAsync_ShouldPreserveImageQuality()`

#### 6.2 Profile Validation
- [ ] `StartResizeAsync_WithInvalidProfile_ShouldThrowException()`
- [ ] `StartResizeAsync_WithZeroDimensions_ShouldThrowException()`
- [ ] `StartResizeAsync_WithNegativeDimensions_ShouldThrowException()`

#### 6.3 Memory Management
- [ ] `StartResizeAsync_WithLargeImages_ShouldNotExceedMemoryLimits()`
- [ ] `StartResizeAsync_ShouldProcessInBatches()`
- [ ] `StartResizeAsync_ShouldStreamDirectlyToDisk()`

#### 6.4 Error Handling
- [ ] `StartResizeAsync_WithCorruptedImages_ShouldSkipAndContinue()`
- [ ] `StartResizeAsync_WithUnsupportedFormats_ShouldSkipAndContinue()`
- [ ] `StartResizeAsync_WithCancellation_ShouldCleanupPartialFiles()`
- [ ] `StartResizeAsync_WithDiskSpaceError_ShouldFailGracefully()`

---

## 7. Image Processing Service Component Tests
**File**: `ImageProcessingServiceComponentTests.cs`
**Infrastructure**: Test Image Files, Memory Streams

### Test Categories

#### 7.1 Image Resize Operations
- [ ] `ResizeImageAsync_WithValidJpeg_ShouldResizeCorrectly()`
- [ ] `ResizeImageAsync_WithValidPng_ShouldResizeCorrectly()`
- [ ] `ResizeImageAsync_WithValidWebp_ShouldResizeCorrectly()`
- [ ] `ResizeImageAsync_ShouldMaintainAspectRatio()`
- [ ] `ResizeImageAsync_ShouldHandlePortraitAndLandscape()`

#### 7.2 Format Handling
- [ ] `ResizeImageAsync_WithUnsupportedFormat_ShouldThrowException()`
- [ ] `ResizeImageAsync_WithCorruptedImage_ShouldThrowException()`
- [ ] `ResizeImageAsync_WithZeroSizeImage_ShouldThrowException()`

#### 7.3 Batch Processing
- [ ] `ProcessImagesAsync_WithMultipleImages_ShouldProcessAll()`
- [ ] `ProcessImagesAsync_ShouldReportProgress()`
- [ ] `ProcessImagesAsync_WithCancellation_ShouldStopProcessing()`
- [ ] `ProcessImagesAsync_WithMixedFormats_ShouldHandleAll()`

#### 7.4 Memory Management
- [ ] `ProcessImagesAsync_WithLargeImages_ShouldNotLeakMemory()`
- [ ] `ResizeImageAsync_ShouldDisposeResourcesProperly()`

---

## 8. Background Task Service Component Tests
**File**: `BackgroundTaskServiceComponentTests.cs`
**Infrastructure**: Test Task Queue, Service Provider Mock

### Test Categories

#### 8.1 Task Execution
- [ ] `ExecuteAsync_ShouldProcessQueuedTasks()`
- [ ] `ExecuteAsync_ShouldHandleTaskExceptions()`
- [ ] `ExecuteAsync_ShouldCreateServiceScopesCorrectly()`
- [ ] `ExecuteAsync_ShouldRespectCancellation()`

#### 8.2 Service Scope Management
- [ ] `TaskExecution_ShouldCreateSeparateScopes()`
- [ ] `TaskExecution_ShouldDisposeServicesProperly()`
- [ ] `TaskExecution_WithScopeCreationFailure_ShouldLogAndContinue()`

#### 8.3 Error Handling
- [ ] `TaskExecution_WithUnhandledException_ShouldLogAndContinue()`
- [ ] `TaskExecution_WithServiceResolutionFailure_ShouldLogAndContinue()`

---

## 9. Task Progress Service Component Tests
**File**: `TaskProgressServiceComponentTests.cs`
**Infrastructure**: SignalR Test Server, Mock Hub Context

### Test Categories

#### 9.1 Progress Notifications
- [ ] `NotifyProgressAsync_ShouldSendToCorrectGroup()`
- [ ] `NotifyProgressAsync_ShouldIncludeAllProgressData()`
- [ ] `NotifyProgressAsync_WithInvalidTaskId_ShouldNotThrow()`

#### 9.2 Completion Notifications
- [ ] `NotifyTaskCompletedAsync_ShouldSendCompletionMessage()`
- [ ] `NotifyTaskErrorAsync_ShouldSendErrorMessage()`

#### 9.3 SignalR Integration
- [ ] `ProgressHub_ConnectionManagement_ShouldWorkCorrectly()`
- [ ] `ProgressHub_JoinGroup_ShouldAddToGroup()`
- [ ] `ProgressHub_LeaveGroup_ShouldRemoveFromGroup()`

---

## 10. Immich Service Component Tests
**File**: `ImmichServiceComponentTests.cs`
**Infrastructure**: Mock HTTP Server (WireMock)

### Test Categories

#### 10.1 Configuration
- [ ] `Configure_WithValidCredentials_ShouldSetupCorrectly()`
- [ ] `Configure_WithInvalidUrl_ShouldThrowException()`
- [ ] `Configure_WithEmptyApiKey_ShouldThrowException()`

#### 10.2 Connection Validation
- [ ] `ValidateConnectionAsync_WithValidServer_ShouldReturnTrue()`
- [ ] `ValidateConnectionAsync_WithInvalidCredentials_ShouldReturnFalse()`
- [ ] `ValidateConnectionAsync_WithServerDown_ShouldReturnFalse()`
- [ ] `ValidateConnectionAsync_WithTimeout_ShouldReturnFalse()`

#### 10.3 Album Operations
- [ ] `GetAlbumsAsync_WithValidResponse_ShouldReturnAlbums()`
- [ ] `GetAlbumsAsync_WithEmptyResponse_ShouldReturnEmptyList()`
- [ ] `GetAlbumsAsync_WithMalformedResponse_ShouldThrowException()`
- [ ] `GetAlbumInfoAsync_WithValidId_ShouldReturnAlbumDetails()`
- [ ] `GetAlbumInfoAsync_WithInvalidId_ShouldThrowException()`

#### 10.4 Asset Download
- [ ] `DownloadAssetAsync_WithValidAsset_ShouldReturnStream()`
- [ ] `DownloadAssetAsync_WithInvalidAsset_ShouldThrowException()`
- [ ] `DownloadAssetAsync_WithLargeAsset_ShouldStreamCorrectly()`

#### 10.5 Error Handling
- [ ] `ApiCalls_WithRateLimiting_ShouldHandleCorrectly()`
- [ ] `ApiCalls_WithNetworkErrors_ShouldRetryAndFail()`
- [ ] `ApiCalls_WithAuthenticationErrors_ShouldThrowAppropriateException()`

---

## 11. Database Integration Component Tests
**File**: `DatabaseIntegrationComponentTests.cs`
**Infrastructure**: PostgreSQL TestContainer

### Test Categories

#### 11.1 Entity Relationships
- [ ] `UserAlbumRelationship_ShouldMaintainReferentialIntegrity()`
- [ ] `AlbumAssetRelationship_ShouldCascadeDeletes()`
- [ ] `ProfileTaskRelationship_ShouldPreventDeletionWhenInUse()`

#### 11.2 Configuration Management
- [ ] `AppSettings_ShouldSupportKeyValueStorage()`
- [ ] `AppSettings_ShouldHandleNullValues()`
- [ ] `AppSettings_ShouldAllowUpdates()`

#### 11.3 Background Task Tracking
- [ ] `BackgroundTasks_ShouldTrackProgress()`
- [ ] `BackgroundTasks_ShouldHandleCompletion()`
- [ ] `BackgroundTasks_ShouldCleanupOldTasks()`

#### 11.4 Migration and Schema
- [ ] `DatabaseMigration_ShouldCreateCorrectSchema()`
- [ ] `DatabaseIndexes_ShouldOptimizeCommonQueries()`

---

## 12. End-to-End Workflow Component Tests
**File**: `E2EWorkflowComponentTests.cs`
**Infrastructure**: Full stack (PostgreSQL, Mock Immich, File System)

### Test Categories

#### 12.1 Complete Download Workflow
- [ ] `FullDownloadWorkflow_FromAlbumSelectionToZipDownload_ShouldWork()`
- [ ] `FullResizeWorkflow_FromDownloadToResizedZip_ShouldWork()`

#### 12.2 Multi-User Scenarios
- [ ] `ConcurrentDownloads_ByDifferentUsers_ShouldNotInterfere()`
- [ ] `UserIsolation_ShouldPreventCrossUserAccess()`

#### 12.3 Long-Running Operations
- [ ] `LongRunningDownload_ShouldMaintainProgress()`
- [ ] `LongRunningResize_ShouldHandleCancellation()`

---

## 13. Performance Component Tests
**File**: `PerformanceComponentTests.cs`
**Infrastructure**: Large Test Datasets, Performance Counters

### Test Categories

#### 13.1 Memory Usage
- [ ] `LargeAlbumDownload_ShouldNotExceedMemoryThreshold()`
- [ ] `ConcurrentOperations_ShouldManageMemoryEfficiently()`

#### 13.2 File System Performance
- [ ] `HighVolumeDownloads_ShouldMaintainPerformance()`
- [ ] `ParallelFileAccess_ShouldNotCauseContention()`

#### 13.3 Database Performance
- [ ] `LargeDatasetQueries_ShouldCompleteWithinTimeLimit()`
- [ ] `ConcurrentDatabaseAccess_ShouldNotDeadlock()`

---

## 14. Security Component Tests
**File**: `SecurityComponentTests.cs`
**Infrastructure**: Security Test Scenarios

### Test Categories

#### 14.1 Authentication Security
- [ ] `JwtTokens_ShouldExpireCorrectly()`
- [ ] `PasswordHashing_ShouldUseBCryptSecurely()`
- [ ] `AuthenticationBypass_ShouldNotBePossible()`

#### 14.2 File Access Security
- [ ] `FileAccess_ShouldPreventDirectoryTraversal()`
- [ ] `DownloadAccess_ShouldRespectUserOwnership()`

#### 14.3 Input Validation
- [ ] `ApiInputs_ShouldValidateAndSanitize()`
- [ ] `FileUploads_ShouldValidateFileTypes()`

---

## Implementation Priority

### Phase 1 (Critical - Week 1)
1. AuthControllerComponentTests
2. StreamingDownloadServiceComponentTests  
3. ImmichServiceComponentTests
4. BasicAuthComponentTests (already implemented)

### Phase 2 (Core Functionality - Week 2)
5. AlbumsControllerComponentTests
6. ConfigControllerComponentTests
7. TasksControllerComponentTests
8. StreamingResizeServiceComponentTests

### Phase 3 (Additional Features - Week 3)
9. ImageProcessingServiceComponentTests
10. BackgroundTaskServiceComponentTests
11. TaskProgressServiceComponentTests
12. DatabaseIntegrationComponentTests

### Phase 4 (Advanced Testing - Week 4)
13. E2EWorkflowComponentTests
14. PerformanceComponentTests
15. SecurityComponentTests

---

## Testing Infrastructure Requirements

### Mock Services Needed
- Mock Immich Server (WireMock.Net) ✅ Already implemented
- Mock HTTP clients for external calls
- Mock file system for file operations testing
- Mock SignalR hub context

### Test Data Requirements
- Sample album data with various asset counts
- Test images in multiple formats (JPEG, PNG, WebP)
- Large dataset generators for performance testing
- Invalid/corrupted file samples for error testing

### Infrastructure Components
- PostgreSQL TestContainers ✅ Already implemented
- Temporary file system management
- Test-specific configuration overrides
- Performance monitoring hooks

---

## Success Metrics

### Coverage Targets
- **Line Coverage**: 85%+ for all tested components
- **Branch Coverage**: 80%+ for critical paths
- **Integration Coverage**: 100% of external integration points

### Performance Benchmarks
- Memory usage should not exceed 500MB during large operations
- Download operations should maintain >10 MB/s throughput
- Database operations should complete within 100ms for standard queries

### Reliability Standards
- All tests should pass consistently (99%+ success rate)
- No test should take longer than 5 minutes to complete
- Tests should be independent and parallelizable