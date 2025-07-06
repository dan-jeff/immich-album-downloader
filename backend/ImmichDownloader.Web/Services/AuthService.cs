using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using BCrypt.Net;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Implementation of the authentication service for user management and JWT token generation.
/// This service handles user registration, authentication, password hashing, and JWT token creation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;

    /// <summary>
    /// Initializes a new instance of the AuthService class.
    /// </summary>
    /// <param name="context">The database context for user data operations.</param>
    /// <param name="jwtService">The JWT service for secure token operations.</param>
    public AuthService(ApplicationDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Checks if any users exist in the system.
    /// This is typically used during application setup to determine if an initial user needs to be created.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains true if any users exist, false otherwise.
    /// </returns>
    /// <exception cref="Exception">Thrown when the database query fails.</exception>
    public async Task<bool> UserExistsAsync()
    {
        return await _context.Users.AnyAsync();
    }

    /// <summary>
    /// Creates a new user account with the specified username and password.
    /// The password is hashed using BCrypt before being stored in the database.
    /// </summary>
    /// <param name="username">The username for the new account.</param>
    /// <param name="password">The plain text password for the new account.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains true if the user was created successfully, false if a user with the same username already exists.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when username or password is null or empty.</exception>
    /// <exception cref="Exception">Thrown when the database operation fails.</exception>
    public async Task<bool> CreateUserAsync(string username, string password)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        
        // Additional security validations
        if (username.Length < 3 || username.Length > 50)
            throw new ArgumentException("Username must be between 3 and 50 characters", nameof(username));
            
        if (password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters", nameof(password));
            
        if (password.Length > 128)
            throw new ArgumentException("Password must be no more than 128 characters", nameof(password));

        if (await _context.Users.AnyAsync(u => u.Username == username))
            return false;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        
        var user = new User
        {
            Username = username,
            PasswordHash = passwordHash
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return true;
    }

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
    public async Task<string?> AuthenticateAsync(string username, string password)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null)
            return null;
            
        try
        {
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;
        }
        catch (Exception)
        {
            // BCrypt.Verify can throw exceptions with corrupted hashes
            // Return null for authentication failure in such cases
            return null;
        }

        return _jwtService.GenerateToken(user.Id, user.Username);
    }

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
    public async Task<bool> VerifyUserAsync(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null)
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception)
        {
            // BCrypt.Verify can throw exceptions with corrupted hashes
            // Return false for verification failure in such cases
            return false;
        }
    }

}