using FluentAssertions;
using ImmichDownloader.Web.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ImmichDownloader.Tests.Security;

/// <summary>
/// Security integration tests for file access controls and path validation.
/// Validates protection against directory traversal attacks and unauthorized file access.
/// </summary>
[Trait("Category", "SecurityTest")]
public class FileAccessSecurityTests : IDisposable
{
    private readonly SecureFileService _secureFileService;
    private readonly Mock<ILogger<SecureFileService>> _loggerMock;
    private readonly string _tempDirectory;
    private readonly string _allowedBasePath;

    public FileAccessSecurityTests()
    {
        _loggerMock = new Mock<ILogger<SecureFileService>>();
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        
        // Setup configuration to use temp directories instead of /app
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FileAccessSecurityTests", Guid.NewGuid().ToString());
        configMock.Setup(c => c["SecureDirectories:Downloads"]).Returns(Path.Combine(_tempDirectory, "downloads"));
        configMock.Setup(c => c["SecureDirectories:Temp"]).Returns(Path.Combine(_tempDirectory, "temp"));
        
        _secureFileService = new SecureFileService(_loggerMock.Object, configMock.Object);
        
        _allowedBasePath = Path.Combine(_tempDirectory, "allowed");
        
        Directory.CreateDirectory(_allowedBasePath);
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "forbidden"));
        
        // Create test files
        File.WriteAllText(Path.Combine(_allowedBasePath, "allowed-file.txt"), "This file should be accessible");
        File.WriteAllText(Path.Combine(_tempDirectory, "forbidden", "forbidden-file.txt"), "This file should NOT be accessible");
        File.WriteAllText(Path.Combine(_tempDirectory, "root-file.txt"), "This file should NOT be accessible");
    }

    #region Path Validation Tests

    [Fact]
    public void ValidateFilePath_WithValidPath_ShouldPass()
    {
        // Arrange
        var validPath = Path.Combine(_allowedBasePath, "allowed-file.txt");

        // Act
        var result = _secureFileService.ValidateFilePath(validPath, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("../forbidden/forbidden-file.txt")]
    [InlineData("../../root-file.txt")]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("../../../../proc/version")]
    public void ValidateFilePath_WithDirectoryTraversalAttempts_ShouldReject(string maliciousPath)
    {
        // Arrange
        var fullMaliciousPath = Path.Combine(_allowedBasePath, maliciousPath);

        // Act
        var result = _secureFileService.ValidateFilePath(fullMaliciousPath, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Contains("outside allowed directory") || 
            e.Contains("File path contains URL encoding") ||
            e.Contains("File path contains Windows-style path separators") ||
            e.Contains("suspicious traversal patterns") ||
            e.Contains("Symbolic links are not allowed"));
    }

    [Theory]
    [InlineData("allowed-file.txt")]
    [InlineData("subfolder/nested-file.txt")]
    [InlineData("./allowed-file.txt")]
    [InlineData("subfolder/../allowed-file.txt")]
    public void ValidateFilePath_WithValidRelativePaths_ShouldPass(string relativePath)
    {
        // Arrange
        var fullPath = Path.Combine(_allowedBasePath, relativePath);
        
        // Create nested directory and file if needed
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        if (!File.Exists(fullPath))
        {
            File.WriteAllText(fullPath, "test content");
        }

        // Act
        var result = _secureFileService.ValidateFilePath(fullPath, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidateFilePath_WithInvalidInput_ShouldReject(string? invalidPath)
    {
        // Act
        var result = _secureFileService.ValidateFilePath(invalidPath!, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion

    #region URL-Encoded Path Injection Tests

    [Theory]
    [InlineData("%2e%2e%2f%2e%2e%2fforbidden%2fforbidden-file.txt")] // ../.. encoded
    [InlineData("%2e%2e%5c%2e%2e%5cforbidden%5cforbidden-file.txt")] // ..\..\forbidden encoded
    [InlineData("..%2fforbidden%2fforbidden-file.txt")] // ../forbidden encoded
    [InlineData("..%5cforbidden%5cforbidden-file.txt")] // ..\forbidden encoded
    public void ValidateFilePath_WithUrlEncodedTraversal_ShouldReject(string encodedMaliciousPath)
    {
        // Arrange
        var decodedPath = Uri.UnescapeDataString(encodedMaliciousPath);
        var fullPath = Path.Combine(_allowedBasePath, decodedPath);

        // Act
        var result = _secureFileService.ValidateFilePath(fullPath, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Contains("outside allowed directory") || 
            e.Contains("File path contains URL encoding") ||
            e.Contains("File path contains Windows-style path separators") ||
            e.Contains("suspicious traversal patterns") ||
            e.Contains("Symbolic links are not allowed"));
    }

    #endregion

    #region Unicode and Special Character Tests

    [Theory]
    [InlineData("..\\forbidden\\forbidden-file.txt")] // Backslash separators
    [InlineData("../forbidden/forbidden-file.txt")] // Forward slash separators
    [InlineData("..\\\\forbidden\\\\forbidden-file.txt")] // Double backslashes
    [InlineData("..//forbidden//forbidden-file.txt")] // Double forward slashes
    public void ValidateFilePath_WithMixedPathSeparators_ShouldReject(string pathWithMixedSeparators)
    {
        // Arrange
        var fullPath = Path.Combine(_allowedBasePath, pathWithMixedSeparators);

        // Act
        var result = _secureFileService.ValidateFilePath(fullPath, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.Contains("outside allowed directory") || 
            e.Contains("File path contains URL encoding") ||
            e.Contains("File path contains Windows-style path separators") ||
            e.Contains("suspicious traversal patterns") ||
            e.Contains("Symbolic links are not allowed"));
    }

    [Theory]
    [InlineData("file\0name.txt")] // Null byte injection
    [InlineData("filename.txt\0.jpg")] // Null byte extension confusion
    [InlineData("file\x0aname.txt")] // Line feed injection
    [InlineData("file\x0dname.txt")] // Carriage return injection
    public void ValidateFilePath_WithNullByteInjection_ShouldReject(string pathWithNullBytes)
    {
        // Arrange
        var fullPath = Path.Combine(_allowedBasePath, pathWithNullBytes);

        // Act
        var result = _secureFileService.ValidateFilePath(fullPath, _allowedBasePath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion

    #region Symbolic Link and Junction Tests

    [Fact]
    public void ValidateFilePath_WithSymbolicLinkToForbiddenArea_ShouldDetectAndReject()
    {
        // Arrange
        var symlinkPath = Path.Combine(_allowedBasePath, "malicious-symlink.txt");
        var targetPath = Path.Combine(_tempDirectory, "forbidden", "forbidden-file.txt");

        try
        {
            // Try to create a symbolic link (may require admin privileges on Windows)
            if (OperatingSystem.IsWindows())
            {
                // On Windows, skip if we can't create symbolic links
                try
                {
                    File.CreateSymbolicLink(symlinkPath, targetPath);
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip test on Windows if no admin privileges
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Skip if symbolic links not supported
                    return;
                }
            }
            else
            {
                // On Unix-like systems, this should work
                File.CreateSymbolicLink(symlinkPath, targetPath);
            }

            // Act
            var result = _secureFileService.ValidateFilePath(symlinkPath, _allowedBasePath);

            // Assert
            result.IsValid.Should().BeFalse("Symbolic links to forbidden areas should be rejected");
            result.Errors.Should().Contain(e => 
            e.Contains("outside allowed directory") || 
            e.Contains("File path contains URL encoding") ||
            e.Contains("File path contains Windows-style path separators") ||
            e.Contains("suspicious traversal patterns") ||
            e.Contains("Symbolic links are not allowed"));
        }
        finally
        {
            // Cleanup
            if (File.Exists(symlinkPath))
            {
                File.Delete(symlinkPath);
            }
        }
    }

    #endregion

    #region Logging and Monitoring Tests

    [Fact]
    public void ValidateFilePath_WithMaliciousAttempt_ShouldLogWarning()
    {
        // Arrange
        var maliciousPath = Path.Combine(_allowedBasePath, "../../../etc/passwd");

        // Act
        _secureFileService.ValidateFilePath(maliciousPath, _allowedBasePath);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Directory traversal attempt detected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }


    #endregion

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}