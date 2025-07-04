using System.ComponentModel.DataAnnotations;

namespace ImmichDownloader.Web.Models.Requests;

/// <summary>
/// Request model for user authentication containing login credentials.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}