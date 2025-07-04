using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models.Requests;

/// <summary>
/// Request model for user registration containing username and password validation rules.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Gets or sets the username for the new user account.
    /// Must be between 3-100 characters and contain only alphanumeric characters and underscores.
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain alphanumeric characters and underscores")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for the new user account.
    /// Must be between 8-100 characters and contain at least one uppercase letter, lowercase letter, number, and special character.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 100 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]+$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character")]
    public string Password { get; set; } = string.Empty;
}