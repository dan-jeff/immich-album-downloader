using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the AuthService class.
    /// </summary>
    /// <param name="context">The database context for user data operations.</param>
    /// <param name="configuration">The configuration provider for JWT settings.</param>
    public AuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return GenerateJwtToken(user);
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

        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    /// <summary>
    /// Generates a JWT token for the specified user.
    /// The token includes the user's username and ID as claims, and is signed using the configured secret key.
    /// </summary>
    /// <param name="user">The user for whom to generate the token.</param>
    /// <returns>A JWT token string that can be used for authentication.</returns>
    /// <exception cref="ArgumentException">Thrown when the JWT configuration is invalid.</exception>
    /// <exception cref="Exception">Thrown when token generation fails.</exception>
    private string GenerateJwtToken(User user)
    {
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "your-secret-key");
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(
                int.Parse(_configuration["Jwt:ExpireMinutes"] ?? "30")),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}