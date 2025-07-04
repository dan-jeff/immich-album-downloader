namespace ImmichDownloader.Web.Models.Responses;

/// <summary>
/// Response model containing JWT access token information for authenticated users.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// Gets or sets the JWT access token for API authentication.
    /// </summary>
    public string access_token { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the token type, typically "Bearer" for JWT tokens.
    /// </summary>
    public string token_type { get; set; } = "Bearer";
}