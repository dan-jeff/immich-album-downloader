using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Service interface for user authentication and management.
/// Provides methods for user registration, authentication, and JWT token generation.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Checks if any users exist in the system.
    /// This is typically used during application setup to determine if an initial user needs to be created.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains true if any users exist, false otherwise.
    /// </returns>
    /// <exception cref="Exception">Thrown when the database query fails.</exception>
    Task<bool> UserExistsAsync();

    /// <summary>
    /// Creates a new user account with the specified username and password.
    /// The password is hashed before being stored in the database.
    /// </summary>
    /// <param name="username">The username for the new account.</param>
    /// <param name="password">The plain text password for the new account.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains true if the user was created successfully, false otherwise.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when username or password is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a user with the same username already exists.</exception>
    /// <exception cref="Exception">Thrown when the database operation fails.</exception>
    Task<bool> CreateUserAsync(string username, string password);

    /// <summary>
    /// Authenticates a user with the provided username and password.
    /// If authentication is successful, returns a JWT token for the user.
    /// </summary>
    /// <param name="username">The username to authenticate.</param>
    /// <param name="password">The plain text password to verify.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a JWT token if authentication succeeds, null otherwise.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when username or password is null or empty.</exception>
    /// <exception cref="Exception">Thrown when the database query or token generation fails.</exception>
    Task<string?> AuthenticateAsync(string username, string password);

    /// <summary>
    /// Verifies a user's credentials without generating a token.
    /// This method is used for password verification in scenarios where token generation is not needed.
    /// </summary>
    /// <param name="username">The username to verify.</param>
    /// <param name="password">The plain text password to verify.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains true if the credentials are valid, false otherwise.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when username or password is null or empty.</exception>
    /// <exception cref="Exception">Thrown when the database query fails.</exception>
    Task<bool> VerifyUserAsync(string username, string password);
}

// Request/Response models moved to Models/AuthModels.cs to avoid conflicts