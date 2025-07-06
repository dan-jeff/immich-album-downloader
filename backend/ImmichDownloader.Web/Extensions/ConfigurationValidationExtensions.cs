using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ImmichDownloader.Web.Extensions;

/// <summary>
/// Extension methods for validating application configuration at startup.
/// Ensures all required configuration values are present and valid before the application starts.
/// </summary>
public static class ConfigurationValidationExtensions
{
    /// <summary>
    /// Validates all required configuration settings at application startup.
    /// Throws exceptions if critical configuration is missing or invalid.
    /// </summary>
    /// <param name="services">The service collection to add validation to.</param>
    /// <param name="configuration">The configuration to validate.</param>
    /// <param name="environment">The hosting environment for environment-specific validation.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing or invalid.</exception>
    public static IServiceCollection AddConfigurationValidation(
        this IServiceCollection services, 
        IConfiguration configuration, 
        IWebHostEnvironment environment)
    {
        var validationErrors = new List<string>();

        // Validate JWT configuration
        ValidateJwtConfiguration(configuration, environment, validationErrors);
        
        // Validate database configuration
        ValidateDatabaseConfiguration(configuration, validationErrors);
        
        // Validate CORS configuration for production
        if (environment.IsProduction())
        {
            ValidateProductionCorsConfiguration(configuration, validationErrors);
        }
        
        // Validate file storage configuration
        ValidateFileStorageConfiguration(configuration, validationErrors);

        // Throw aggregated validation errors
        if (validationErrors.Any())
        {
            var errorMessage = "Application configuration validation failed:\n" + 
                              string.Join("\n", validationErrors.Select(e => $"- {e}"));
            throw new InvalidOperationException(errorMessage);
        }

        return services;
    }

    /// <summary>
    /// Validates JWT configuration settings.
    /// </summary>
    private static void ValidateJwtConfiguration(IConfiguration configuration, IWebHostEnvironment environment, List<string> errors)
    {
        // Check for JWT key
        var jwtKey = configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            errors.Add("JWT secret key is required. Set JWT_SECRET_KEY environment variable or Jwt:Key configuration.");
            return;
        }

        // Validate key strength in production
        if (environment.IsProduction())
        {
            if (jwtKey.Length < 32)
            {
                errors.Add("JWT secret key must be at least 32 characters in production.");
            }
            
            if (jwtKey.Contains("CHANGE_THIS") || jwtKey.Contains("development") || jwtKey.Contains("test"))
            {
                errors.Add("JWT secret key appears to be a placeholder or development key. Use a secure random key in production.");
            }
        }

        // Validate other JWT settings
        var issuer = configuration["Jwt:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            errors.Add("JWT Issuer is required in configuration.");
        }

        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            errors.Add("JWT Audience is required in configuration.");
        }

        // Validate expiration time
        if (!int.TryParse(configuration["Jwt:ExpireMinutes"], out var expireMinutes) || expireMinutes <= 0)
        {
            errors.Add("JWT ExpireMinutes must be a positive integer.");
        }
        else if (environment.IsProduction() && expireMinutes > 480) // 8 hours max in production
        {
            errors.Add("JWT ExpireMinutes should not exceed 480 minutes (8 hours) in production for security.");
        }
    }

    /// <summary>
    /// Validates database configuration settings.
    /// </summary>
    private static void ValidateDatabaseConfiguration(IConfiguration configuration, List<string> errors)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("Database connection string 'DefaultConnection' is required.");
        }
    }

    /// <summary>
    /// Validates CORS configuration for production environments.
    /// </summary>
    private static void ValidateProductionCorsConfiguration(IConfiguration configuration, List<string> errors)
    {
        var allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
        var allowedOrigins = !string.IsNullOrEmpty(allowedOriginsEnv)
            ? allowedOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            : configuration.GetSection("AllowedOrigins").Get<string[]>();

        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            errors.Add("CORS allowed origins must be explicitly configured in production via ALLOWED_ORIGINS environment variable.");
        }
        else
        {
            // Check for development origins in production
            var developmentOrigins = allowedOrigins.Where(origin => 
                origin.Contains("localhost") || origin.Contains("127.0.0.1")).ToArray();
            
            if (developmentOrigins.Any())
            {
                errors.Add($"Production CORS configuration contains development origins: {string.Join(", ", developmentOrigins)}");
            }
        }
    }

    /// <summary>
    /// Validates file storage configuration settings.
    /// </summary>
    private static void ValidateFileStorageConfiguration(IConfiguration configuration, List<string> errors)
    {
        var dataPath = configuration["DataPath"];
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            errors.Add("DataPath configuration is required.");
        }

        var downloadsPath = configuration["FileStorage:DownloadsPath"];
        if (string.IsNullOrWhiteSpace(downloadsPath))
        {
            errors.Add("FileStorage:DownloadsPath configuration is required.");
        }

        var resizedPath = configuration["FileStorage:ResizedPath"];
        if (string.IsNullOrWhiteSpace(resizedPath))
        {
            errors.Add("FileStorage:ResizedPath configuration is required.");
        }
    }
}