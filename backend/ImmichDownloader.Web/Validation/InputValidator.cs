using System.Text.RegularExpressions;
using System.Web;

namespace ImmichDownloader.Web.Validation;

/// <summary>
/// Provides comprehensive input validation and sanitization to prevent security vulnerabilities.
/// Implements validation for common attack vectors including directory traversal, injection attacks,
/// and malformed input data.
/// </summary>
public static class InputValidator
{
    private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9_\-\.]{3,50}$", RegexOptions.Compiled);
    private static readonly Regex AlbumNameRegex = new(@"^[a-zA-Z0-9\s\-_\.\(\)\[\]]{1,255}$", RegexOptions.Compiled);
    private static readonly Regex TaskIdRegex = new(@"^[a-zA-Z0-9\-]{36}$", RegexOptions.Compiled); // GUID format
    private static readonly Regex ApiKeyRegex = new(@"^[a-zA-Z0-9]{20,128}$", RegexOptions.Compiled);
    
    // Common directory traversal patterns
    private static readonly string[] DirectoryTraversalPatterns = 
    {
        "..", "/..", "\\..", "..\\", "../", "..\\",
        "%2e%2e", "%2e%2e%2f", "%2e%2e%5c",
        "0x2e0x2e", "0x2e0x2e0x2f", "0x2e0x2e0x5c"
    };
    
    // Common injection patterns
    private static readonly string[] InjectionPatterns =
    {
        "<script", "</script>", "javascript:", "vbscript:",
        "onclick", "onload", "onerror", "onmouseover",
        "expression(", "url(", "@import", "behavior:",
        "';", "\";", "/*", "*/", "--", "xp_", "sp_"
    };

    /// <summary>
    /// Validates a username for registration and authentication.
    /// Ensures the username contains only safe characters and meets length requirements.
    /// </summary>
    /// <param name="username">The username to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;
            
        return UsernameRegex.IsMatch(username) && !ContainsInjectionPatterns(username);
    }

    /// <summary>
    /// Validates a password for security requirements.
    /// Ensures minimum length and complexity without being overly restrictive.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidPassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return false;
            
        // Minimum 8 characters, at least one letter and one number
        if (password.Length < 8)
            return false;
            
        bool hasLetter = password.Any(char.IsLetter);
        bool hasDigit = password.Any(char.IsDigit);
        
        return hasLetter && hasDigit;
    }

    /// <summary>
    /// Validates an album name for download operations.
    /// Prevents directory traversal and ensures safe file system operations.
    /// </summary>
    /// <param name="albumName">The album name to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidAlbumName(string? albumName)
    {
        if (string.IsNullOrWhiteSpace(albumName))
            return false;
            
        // Check basic format
        if (!AlbumNameRegex.IsMatch(albumName))
            return false;
            
        // Check for directory traversal
        if (ContainsDirectoryTraversal(albumName))
            return false;
            
        // Check for injection patterns
        if (ContainsInjectionPatterns(albumName))
            return false;
            
        return true;
    }

    /// <summary>
    /// Validates a task ID to ensure it's a proper GUID format.
    /// Prevents manipulation of task references and ensures data integrity.
    /// </summary>
    /// <param name="taskId">The task ID to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidTaskId(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            return false;
            
        return TaskIdRegex.IsMatch(taskId);
    }

    /// <summary>
    /// Validates an Immich API key format.
    /// Ensures the API key meets expected format requirements.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;
            
        return ApiKeyRegex.IsMatch(apiKey);
    }

    /// <summary>
    /// Validates a URL for Immich server configuration.
    /// Ensures the URL is well-formed and uses appropriate protocols.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
            
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
            
        // Only allow HTTP and HTTPS protocols
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
            
        // Prevent localhost/private IP access in production (should be configurable)
        var host = uri.Host.ToLowerInvariant();
        if (host == "localhost" || host == "127.0.0.1" || host.StartsWith("192.168.") || 
            host.StartsWith("10.") || host.StartsWith("172."))
        {
            // Allow in development/testing environments
            // In production, this should be configurable
        }
        
        return true;
    }

    /// <summary>
    /// Validates a file path to ensure it's within allowed directories.
    /// Prevents directory traversal attacks and unauthorized file access.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="allowedBasePath">The base path that files must be within.</param>
    /// <returns>True if the path is safe, false otherwise.</returns>
    public static bool IsValidFilePath(string? filePath, string allowedBasePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(allowedBasePath))
            return false;
            
        try
        {
            // Normalize paths - combine filename with base path first
            var combinedPath = Path.Combine(allowedBasePath, filePath);
            var fullPath = Path.GetFullPath(combinedPath);
            var basePath = Path.GetFullPath(allowedBasePath);
            
            // Ensure the file path is within the allowed base path
            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If path operations fail, assume unsafe
            return false;
        }
    }

    /// <summary>
    /// Sanitizes a string for safe output, preventing XSS attacks.
    /// Encodes HTML entities and removes potentially dangerous content.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>A sanitized string safe for output.</returns>
    public static string SanitizeForOutput(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
            
        // HTML encode the input
        var sanitized = HttpUtility.HtmlEncode(input);
        
        // Remove or escape any remaining dangerous patterns
        foreach (var pattern in InjectionPatterns)
        {
            sanitized = sanitized.Replace(pattern, "", StringComparison.OrdinalIgnoreCase);
        }
        
        return sanitized;
    }

    /// <summary>
    /// Sanitizes a filename for safe file system operations.
    /// Removes invalid characters and prevents directory traversal.
    /// </summary>
    /// <param name="fileName">The filename to sanitize.</param>
    /// <returns>A sanitized filename safe for file system operations.</returns>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "unknown";
            
        // Remove directory traversal attempts
        var sanitized = fileName;
        foreach (var pattern in DirectoryTraversalPatterns)
        {
            sanitized = sanitized.Replace(pattern, "_");
        }
        
        // Remove invalid file name characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '_');
        }
        
        // Remove additional dangerous characters
        sanitized = sanitized.Replace('/', '_').Replace('\\', '_');
        
        // Limit length
        if (sanitized.Length > 255)
        {
            var extension = Path.GetExtension(sanitized);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            sanitized = nameWithoutExtension[..(255 - extension.Length)] + extension;
        }
        
        // Ensure not empty after sanitization
        return string.IsNullOrWhiteSpace(sanitized) ? "sanitized_file" : sanitized;
    }

    /// <summary>
    /// Validates request data for API endpoints using comprehensive validation rules.
    /// </summary>
    /// <param name="data">The data object to validate.</param>
    /// <returns>A validation result with any errors found.</returns>
    public static ValidationResult ValidateRequestData(object data)
    {
        var result = new ValidationResult();
        
        if (data == null)
        {
            result.Errors.Add("Request data cannot be null");
            return result;
        }
        
        // Use reflection to validate properties based on attributes or naming conventions
        var properties = data.GetType().GetProperties();
        
        foreach (var property in properties)
        {
            var value = property.GetValue(data)?.ToString();
            
            switch (property.Name.ToLowerInvariant())
            {
                case "username":
                    if (!IsValidUsername(value))
                        result.Errors.Add("Username contains invalid characters or format");
                    break;
                    
                case "password":
                    if (!IsValidPassword(value))
                        result.Errors.Add("Password must be at least 8 characters with letters and numbers");
                    break;
                    
                case "albumname":
                    if (!IsValidAlbumName(value))
                        result.Errors.Add("Album name contains invalid characters");
                    break;
                    
                case "taskid":
                    if (!IsValidTaskId(value))
                        result.Errors.Add("Task ID must be a valid GUID format");
                    break;
                    
                case "immichurl":
                case "url":
                    if (!IsValidUrl(value))
                        result.Errors.Add("URL must be a valid HTTP or HTTPS URL");
                    break;
                    
                case "apikey":
                    if (!IsValidApiKey(value))
                        result.Errors.Add("API key format is invalid");
                    break;
            }
        }
        
        return result;
    }

    /// <summary>
    /// Checks if a string contains directory traversal patterns.
    /// </summary>
    private static bool ContainsDirectoryTraversal(string input)
    {
        var lowerInput = input.ToLowerInvariant();
        return DirectoryTraversalPatterns.Any(pattern => 
            lowerInput.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a string contains common injection patterns.
    /// </summary>
    private static bool ContainsInjectionPatterns(string input)
    {
        var lowerInput = input.ToLowerInvariant();
        return InjectionPatterns.Any(pattern => 
            lowerInput.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Represents the result of input validation with any errors found.
/// </summary>
public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; } = new();
}