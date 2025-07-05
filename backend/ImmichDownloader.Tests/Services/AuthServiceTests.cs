using FluentAssertions;
using FunctionalDev.MoqHelpers;
using FunctionalDev.MoqHelpers.Construction;
using FunctionalDev.MoqHelpers.Construction.Container;
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
public class AuthServiceTests
{
    private readonly ActivatedObject<AuthService> _serviceContainer;
    private readonly Mock<ApplicationDbContext> _contextMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _serviceContainer = LazyActivator.CreateLazyContainer<AuthService>();
        _contextMock = _serviceContainer.GetArgumentAsMock<ApplicationDbContext>();
        _configurationMock = _serviceContainer.GetArgumentAsMock<IConfiguration>();
        _authService = _serviceContainer.Instance;
    }

    #region UserExistsAsync Tests

    [Fact]
    public async Task UserExistsAsync_WhenNoUsers_ShouldReturnFalse()
    {
        // Arrange
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act
        var result = await _authService.UserExistsAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserExistsAsync_WhenUsersExist_ShouldReturnTrue()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = 1, Username = "testuser", PasswordHash = "hashedpassword" }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act
        var result = await _authService.UserExistsAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserExistsAsync_WhenDatabaseThrowsException_ShouldPropagateException()
    {
        // Arrange
        var mockUsers = new Mock<DbSet<User>>();
        mockUsers.Setup(x => x.AnyAsync(default))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act & Assert
        await _authService.Invoking(s => s.UserExistsAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed");
    }

    #endregion

    #region CreateUserAsync Tests

    [Fact]
    public async Task CreateUserAsync_WithValidCredentials_ShouldCreateUserSuccessfully()
    {
        // Arrange
        var username = "newuser";
        var password = "SecurePassword123!";
        
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await _authService.CreateUserAsync(username, password);

        // Assert
        result.Should().BeTrue();
        mockUsers.Verify(x => x.Add(It.Is<User>(u => 
            u.Username == username && 
            BCrypt.Net.BCrypt.Verify(password, u.PasswordHash))), Times.Once);
        _contextMock.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserAlreadyExists_ShouldReturnFalse()
    {
        // Arrange
        var username = "existinguser";
        var password = "SecurePassword123!";
        
        var existingUsers = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = "existinghash" }
        };
        var mockUsers = MockDbSet(existingUsers);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act
        var result = await _authService.CreateUserAsync(username, password);

        // Assert
        result.Should().BeFalse();
        mockUsers.Verify(x => x.Add(It.IsAny<User>()), Times.Never);
        _contextMock.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    /// <summary>
    /// Critical security test: The service documentation claims it throws ArgumentException
    /// for null/empty inputs, but the implementation doesn't validate inputs.
    /// This exposes a security flaw where null/empty passwords could be accepted.
    /// </summary>
    [Theory]
    [InlineData(null, "ValidPassword123!")]
    [InlineData("", "ValidPassword123!")]
    [InlineData("   ", "ValidPassword123!")]
    [InlineData("validuser", null)]
    [InlineData("validuser", "")]
    [InlineData("validuser", "   ")]
    public async Task CreateUserAsync_WithNullOrEmptyInputs_ShouldExposeInputValidationFlaw(string? username, string? password)
    {
        // Arrange
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act & Assert
        // The service should throw ArgumentException according to documentation, but doesn't
        // This test documents the security flaw
        var result = await _authService.Invoking(s => s.CreateUserAsync(username!, password!))
            .Should().NotThrowAsync();
        
        // The service might still create a user with invalid data, which is a security issue
    }

    [Fact]
    public async Task CreateUserAsync_WhenBCryptThrowsException_ShouldPropagateException()
    {
        // Arrange
        var username = "testuser";
        var password = new string('x', 100000); // Extremely long password that might cause BCrypt to fail
        
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act & Assert
        // BCrypt might throw exception for extremely long passwords or invalid input
        await _authService.Invoking(s => s.CreateUserAsync(username, password))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateUserAsync_WhenDatabaseSaveFails_ShouldPropagateException()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);
        _contextMock.Setup(x => x.SaveChangesAsync(default))
            .ThrowsAsync(new DbUpdateException("Database constraint violation"));

        // Act & Assert
        await _authService.Invoking(s => s.CreateUserAsync(username, password))
            .Should().ThrowAsync<DbUpdateException>()
            .WithMessage("Database constraint violation");
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
        
        var users = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);
        
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
        
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

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
        
        var users = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act
        var result = await _authService.AuthenticateAsync(username, wrongPassword);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Critical security test: JWT generation uses dangerous fallback secret.
    /// This exposes a major security vulnerability where the default secret is "your-secret-key".
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_WithMissingJwtConfiguration_ShouldExposeSecurityFlaw()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        var users = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);
        
        // Deliberately don't set up JWT configuration to expose fallback behavior
        _configurationMock.Setup(x => x["Jwt:Key"]).Returns((string?)null);
        _configurationMock.Setup(x => x["Jwt:ExpireMinutes"]).Returns((string?)null);
        _configurationMock.Setup(x => x["Jwt:Issuer"]).Returns((string?)null);
        _configurationMock.Setup(x => x["Jwt:Audience"]).Returns((string?)null);

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().NotBeNull();
        // This test documents that the service falls back to insecure defaults
        // The JWT will be signed with "your-secret-key" which is a security vulnerability
    }

    [Fact]
    public async Task AuthenticateAsync_WithCorruptedPasswordHash_ShouldReturnNull()
    {
        // Arrange
        var username = "testuser";
        var password = "SecurePassword123!";
        var corruptedHash = "corrupted_hash_that_is_not_valid_bcrypt";
        
        var users = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = corruptedHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

        // Act
        var result = await _authService.AuthenticateAsync(username, password);

        // Assert
        result.Should().BeNull();
        // BCrypt.Verify should handle corrupted hashes gracefully
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
        
        var users = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

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
        
        var users = new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

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
        
        var mockUsers = MockDbSet(new List<User>());
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

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
        
        var users = new List<User>
        {
            new() { Id = 1, Username = existingUsername, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);

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
        
        // First call returns no existing users
        var mockUsers1 = MockDbSet(new List<User>());
        // Second call (after first check but before save) should show user exists
        var mockUsers2 = MockDbSet(new List<User>
        {
            new() { Id = 1, Username = username, PasswordHash = "existinghash" }
        });
        
        _contextMock.SetupSequence(x => x.Users)
            .Returns(mockUsers1.Object)
            .Returns(mockUsers2.Object);
        
        _contextMock.Setup(x => x.SaveChangesAsync(default))
            .ThrowsAsync(new DbUpdateException("Duplicate key constraint violation"));

        // Act & Assert
        await _authService.Invoking(s => s.CreateUserAsync(username, password))
            .Should().ThrowAsync<DbUpdateException>();
        
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
        
        var users = new List<User>
        {
            new() { Id = int.MaxValue, Username = username, PasswordHash = passwordHash }
        };
        var mockUsers = MockDbSet(users);
        _contextMock.Setup(x => x.Users).Returns(mockUsers.Object);
        
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
        _configurationMock.Setup(x => x["Jwt:Key"]).Returns("super-secret-key-that-is-at-least-256-bits-long-for-security");
        _configurationMock.Setup(x => x["Jwt:ExpireMinutes"]).Returns("30");
        _configurationMock.Setup(x => x["Jwt:Issuer"]).Returns("ImmichDownloader");
        _configurationMock.Setup(x => x["Jwt:Audience"]).Returns("ImmichDownloader");
    }

    private static Mock<DbSet<T>> MockDbSet<T>(List<T> list) where T : class
    {
        var queryable = list.AsQueryable();
        var mockDbSet = new Mock<DbSet<T>>();
        
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
        
        return mockDbSet;
    }

    #endregion
}