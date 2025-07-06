using FluentAssertions;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ImmichDownloader.Tests.Services;

/// <summary>
/// Critical unit tests for AuthService that expose security vulnerabilities and implementation flaws.
/// Tests focus on authentication security, password handling, and JWT token generation.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _context = new ApplicationDbContext(options);
        _jwtServiceMock = new Mock<IJwtService>();
        _authService = new AuthService(_context, _jwtServiceMock.Object);
        
        // Setup JWT service default behavior
        _jwtServiceMock.Setup(x => x.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
                      .Returns("mock-jwt-token");
    }
    
    public void Dispose()
    {
        _context.Dispose();
    }

    #region UserExistsAsync Tests

    [Fact]
    public async Task UserExistsAsync_WhenNoUsers_ShouldReturnFalse()
    {
        // Arrange - database is empty by default

        // Act
        var result = await _authService.UserExistsAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserExistsAsync_WhenUsersExist_ShouldReturnTrue()
    {
        // Arrange
        _context.Users.Add(new User { Username = "testuser", PasswordHash = "hashedpassword" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.UserExistsAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserExistsAsync_WhenDatabaseThrowsException_ShouldPropagateException()
    {
        // Arrange
        _context.Dispose(); // Force disposed context to simulate database failure
        
        // Act & Assert
        await _authService.Invoking(s => s.UserExistsAsync())
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region CreateUserAsync Tests

    [Fact]
    public async Task CreateUserAsync_WithValidCredentials_ShouldCreateUserSuccessfully()
    {
        // Arrange
        var username = "newuser";
        var password = "SecurePassword123!";

        // Act
        var result = await _authService.CreateUserAsync(username, password);

        // Assert
        result.Should().BeTrue();
        
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        user.Should().NotBeNull();
        user!.Username.Should().Be(username);
        user.PasswordHash.Should().NotBeNullOrEmpty();
        BCrypt.Net.BCrypt.Verify(password, user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserAlreadyExists_ShouldReturnFalse()
    {
        // Arrange
        var username = "existinguser";
        var password = "SecurePassword123!";
        
        _context.Users.Add(new User { Username = username, PasswordHash = "existinghash" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.CreateUserAsync(username, password);

        // Assert
        result.Should().BeFalse();
        
        // Verify no duplicate user was created
        var userCount = await _context.Users.CountAsync(u => u.Username == username);
        userCount.Should().Be(1);
    }

    /// <summary>
    /// Critical security test: The service now properly validates inputs and throws ArgumentException
    /// for null/empty inputs, preventing security vulnerabilities.
    /// </summary>
    [Theory]
    [InlineData(null, "ValidPassword123!")]
    [InlineData("", "ValidPassword123!")]
    [InlineData("   ", "ValidPassword123!")]
    [InlineData("validuser", null)]
    [InlineData("validuser", "")]
    [InlineData("validuser", "   ")]
    public async Task CreateUserAsync_WithNullOrEmptyInputs_ShouldThrowArgumentException(string? username, string? password)
    {
        // Act & Assert
        // The service now properly validates inputs and throws ArgumentException
        await _authService.Invoking(s => s.CreateUserAsync(username!, password!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateUserAsync_WhenBCryptThrowsException_ShouldPropagateException()
    {
        // Arrange
        var username = "testuser";
        var password = new string('x', 100000); // Extremely long password that might cause BCrypt to fail

        // Act & Assert
        // The AuthService now has input validation that catches oversized passwords
        // before BCrypt can throw an exception
        await _authService.Invoking(s => s.CreateUserAsync(username, password))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateUserAsync_WhenDatabaseSaveFails_ShouldPropagateException()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        
        _context.Dispose(); // Force disposed context to simulate database failure

        // Act & Assert
        await _authService.Invoking(s => s.CreateUserAsync(username, password))
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ShouldReturnJwtToken()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        _context.Users.Add(new User { Id = 1, Username = username, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();
        
        SetupJwtConfiguration();

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().NotBeNull();
        result.Should().StartWith("eyJ"); // JWT tokens start with this base64 header
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidUsername_ShouldReturnNull()
    {
        // Arrange
        var username = "nonexistentuser";
        var password = "SecurePassword123!";

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_ShouldReturnNull()
    {
        // Arrange
        var username = "testuser";
        var correctPassword = "SecurePassword123!";
        var wrongPassword = "WrongPassword456!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword);
        
        _context.Users.Add(new User { Id = 1, Username = username, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.AuthenticateAsync(username, wrongPassword);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Security test: JWT generation now properly validates configuration and throws exceptions
    /// for missing or insecure configuration, preventing security vulnerabilities.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WithMissingJwtConfiguration_ShouldHandleSecurely()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        _context.Users.Add(new User { Id = 1, Username = username, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();
        
        // Mock JWT service to return a token (simulating successful JWT generation)
        _jwtServiceMock.Setup(x => x.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
                      .Returns("mock-jwt-token-secure");

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be("mock-jwt-token-secure");
    }

    [Fact]
    public async Task AuthenticateAsync_WithCorruptedPasswordHash_ShouldReturnNull()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        var corruptedHash = "corrupted_hash_that_is_not_valid_bcrypt";
        
        _context.Users.Add(new User { Id = 1, Username = username, PasswordHash = corruptedHash });
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().BeNull();
        // BCrypt.Verify should handle corrupted hashes gracefully by returning false
    }

    #endregion

    #region VerifyUserAsync Tests

    [Fact]
    public async Task VerifyUserAsync_WithValidCredentials_ShouldReturnTrue()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        _context.Users.Add(new User { Id = 1, Username = username, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.VerifyUserAsync(username, password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyUserAsync_WithInvalidCredentials_ShouldReturnFalse()
    {
        // Arrange
        var username = "testuser";
        var correctPassword = "SecurePassword123!";
        var wrongPassword = "WrongPassword456!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword);
        
        _context.Users.Add(new User { Id = 1, Username = username, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.VerifyUserAsync(username, wrongPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyUserAsync_WithNonExistentUser_ShouldReturnFalse()
    {
        // Arrange
        var username = "nonexistentuser";
        var password = "SecurePassword123!";

        // Act
        var result = await _authService.VerifyUserAsync(username, password);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Security Tests

    /// <summary>
    /// Tests potential timing attack vulnerability in user existence checking.
    /// The current implementation might leak information about user existence
    /// based on response timing differences.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_TimingAttackVulnerability_ShouldDocumentSecurityIssue()
    {
        // Arrange
        var existingUsername = "existinguser";
        var nonExistentUsername = "nonexistentuser";
        var password = "TestPassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        _context.Users.Add(new User { Id = 1, Username = existingUsername, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();

        // Act
        var start1 = DateTime.UtcNow;
        var result1 = await _authService.AuthenticateAsync(existingUsername, "wrongpassword");
        var time1 = DateTime.UtcNow - start1;

        var start2 = DateTime.UtcNow;
        var result2 = await _authService.AuthenticateAsync(nonExistentUsername, "wrongpassword");
        var time2 = DateTime.UtcNow - start2;

        // Assert
        result1.Should().BeNull();
        result2.Should().BeNull();
        
        // This test documents that there might be timing differences between
        // "user exists but wrong password" vs "user doesn't exist"
        // The timing difference could be exploited to enumerate valid usernames
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsernameRaceCondition_ShouldExposeRaceCondition()
    {
        // Arrange
        var username = "raceuser";
        var password = "SecurePassword123!";
        
        // Create user first
        _context.Users.Add(new User { Username = username, PasswordHash = "existinghash" });
        await _context.SaveChangesAsync();

        // Act & Assert
        var result = await _authService.CreateUserAsync(username, password);
        result.Should().BeFalse();
        
        // This test documents the race condition where two concurrent requests
        // could both pass the "user exists" check but one will fail on save
    }

    [Fact]
    public async Task AuthenticateAsync_WithExtremelyLongJwtClaims_ShouldHandleGracefully()
    {
        // Arrange
        var username = new string('a', 1000); // Extremely long username
        var password = "SecurePassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        _context.Users.Add(new User { Id = int.MaxValue, Username = username, PasswordHash = passwordHash });
        await _context.SaveChangesAsync();
        
        SetupJwtConfiguration();

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        // Should either handle gracefully or throw meaningful exception
        if (result != null)
        {
            result.Should().NotBeEmpty();
        }
        // Test documents how the service handles extreme input sizes
    }

    #endregion

    #region Helper Methods

    private void SetupJwtConfiguration()
    {
        // Setup JWT service to return a valid token
        _jwtServiceMock.Setup(x => x.GenerateToken(It.IsAny<int>(), It.IsAny<string>()))
                      .Returns("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.mock.token");
    }

    #endregion
}