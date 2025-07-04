using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models;

/// <summary>
/// Represents a user account in the system with authentication credentials.
/// Users can register, authenticate, and access protected resources via JWT tokens.
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// This is the primary key in the users table.
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the username for the user account.
    /// Must be unique across all users and cannot exceed 100 characters.
    /// Used for authentication and display purposes.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the hashed password for the user account.
    /// Stored as a bcrypt hash for security. Never store plain text passwords.
    /// </summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the UTC timestamp when the user account was created.
    /// Automatically set to the current UTC time when a new user is created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}