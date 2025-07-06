using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Models.Requests;
using ImmichDownloader.Web.Services;
using ImmichDownloader.Web.Services.Database;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Controller responsible for managing application configuration including
/// Immich server settings and resize profiles.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class ConfigController : SecureControllerBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IImmichService _immichService;
    private readonly IDatabaseService _databaseService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigController"/> class.
    /// </summary>
    /// <param name="configurationService">The centralized configuration service.</param>
    /// <param name="immichService">The Immich service for server connectivity.</param>
    /// <param name="databaseService">The centralized database service.</param>
    /// <param name="logger">The logger for security monitoring.</param>
    public ConfigController(
        IConfigurationService configurationService, 
        IImmichService immichService,
        IDatabaseService databaseService,
        ILogger<ConfigController> logger) : base(logger)
    {
        _configurationService = configurationService;
        _immichService = immichService;
        _databaseService = databaseService;
    }

    /// <summary>
    /// Retrieves the current application configuration including Immich settings and resize profiles.
    /// </summary>
    /// <returns>The current configuration settings.</returns>
    /// <response code="200">Returns the configuration settings.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var (immichUrl, apiKey) = await _configurationService.GetImmichSettingsAsync();
        var profiles = await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.ResizeProfiles
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        });

        return Ok(new
        {
            immich_url = immichUrl ?? "",
            api_key = apiKey ?? "",
            resize_profiles = profiles.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                width = p.Width,
                height = p.Height,
                include_horizontal = p.IncludeHorizontal,
                include_vertical = p.IncludeVertical,
                created_at = p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
        });
    }

    /// <summary>
    /// Saves the Immich server configuration settings.
    /// </summary>
    /// <param name="config">The Immich configuration containing server URL and API key.</param>
    /// <returns>A response indicating success or failure.</returns>
    /// <response code="200">Configuration saved successfully.</response>
    /// <response code="400">Invalid configuration data.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpPost("config")]
    public async Task<IActionResult> SaveConfig([FromBody] ImmichConfiguration config)
    {
        // Validate input using secure validation framework
        var validationResult = ValidateInput(config);
        if (validationResult != null)
            return validationResult;

        // Save to database using centralized configuration service
        await _configurationService.SetImmichSettingsAsync(config.immich_url, config.api_key);
        
        // Configure the service directly for immediate use
        _immichService.Configure(config.immich_url, config.api_key);
        
        Logger.LogInformation("Configuration saved by user {Username}", GetCurrentUsername());
        return CreateSuccessResponse(new { success = true });
    }

    /// <summary>
    /// Tests the connection to the Immich server with the provided configuration.
    /// </summary>
    /// <param name="config">The Immich configuration to test.</param>
    /// <returns>A response indicating whether the connection test was successful.</returns>
    /// <response code="200">Returns the connection test result.</response>
    /// <response code="400">Invalid configuration data.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpPost("config/test")]
    public async Task<IActionResult> TestConnection([FromBody] ImmichConfiguration config)
    {
        // Validate input using secure validation framework
        var validationResult = ValidateInput(config);
        if (validationResult != null)
            return validationResult;

        Logger.LogInformation("Connection test initiated by user {Username}", GetCurrentUsername());
        var (success, message) = await _immichService.ValidateConnectionAsync(config.immich_url, config.api_key);
        
        if (success)
        {
            Logger.LogInformation("Connection test successful for user {Username}", GetCurrentUsername());
        }
        else
        {
            Logger.LogWarning("Connection test failed for user {Username}: {Message}", GetCurrentUsername(), message);
        }
        
        return Ok(new { success, message });
    }

    /// <summary>
    /// Creates a new resize profile with the specified dimensions and orientation settings.
    /// </summary>
    /// <param name="request">The resize profile request containing profile details.</param>
    /// <returns>A response indicating success and the new profile ID.</returns>
    /// <response code="200">Profile created successfully.</response>
    /// <response code="400">Invalid profile data.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpPost("profiles")]
    public async Task<IActionResult> CreateProfile([FromBody] ResizeProfileRequest request)
    {
        // Validate input using secure validation framework
        var validationResult = ValidateInput(request);
        if (validationResult != null)
            return validationResult;

        var profile = new ResizeProfile
        {
            Name = request.Name,
            Width = request.Width,
            Height = request.Height,
            IncludeHorizontal = request.IncludeHorizontal,
            IncludeVertical = request.IncludeVertical
        };

        var profileId = await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            context.ResizeProfiles.Add(profile);
            await context.SaveChangesAsync();
            return profile.Id;
        });

        Logger.LogInformation("Profile '{ProfileName}' created by user {Username}", request.Name, GetCurrentUsername());
        return CreateSuccessResponse(new { success = true, id = profileId });
    }

    /// <summary>
    /// Updates an existing resize profile with new settings.
    /// </summary>
    /// <param name="profileId">The ID of the profile to update.</param>
    /// <param name="request">The updated profile settings.</param>
    /// <returns>A response indicating success or failure.</returns>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="400">Invalid profile data.</response>
    /// <response code="404">Profile not found.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpPut("profiles/{profileId}")]
    public async Task<IActionResult> UpdateProfile(int profileId, [FromBody] ResizeProfileRequest request)
    {
        // Validate input using secure validation framework
        var validationResult = ValidateInput(request);
        if (validationResult != null)
            return validationResult;

        // Validate profile ID
        if (profileId <= 0)
        {
            Logger.LogWarning("Invalid profile ID {ProfileId} provided by user {Username}", profileId, GetCurrentUsername());
            return BadRequest(new { detail = "Invalid profile ID" });
        }

        var updated = await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var profile = await context.ResizeProfiles.FindAsync(profileId);
            if (profile == null)
                return false;

            profile.Name = request.Name;
            profile.Width = request.Width;
            profile.Height = request.Height;
            profile.IncludeHorizontal = request.IncludeHorizontal;
            profile.IncludeVertical = request.IncludeVertical;

            await context.SaveChangesAsync();
            return true;
        });

        if (!updated)
        {
            Logger.LogWarning("Profile {ProfileId} not found for user {Username}", profileId, GetCurrentUsername());
            return CreateErrorResponse(404, "Profile not found");
        }
        
        Logger.LogInformation("Profile {ProfileId} updated by user {Username}", profileId, GetCurrentUsername());
        return CreateSuccessResponse(new { success = true });
    }

    /// <summary>
    /// Deletes a resize profile from the system.
    /// </summary>
    /// <param name="profileId">The ID of the profile to delete.</param>
    /// <returns>A response indicating success or failure.</returns>
    /// <response code="200">Profile deleted successfully.</response>
    /// <response code="404">Profile not found.</response>
    /// <response code="401">Unauthorized access.</response>
    [HttpDelete("profiles/{profileId}")]
    public async Task<IActionResult> DeleteProfile(int profileId)
    {
        // Validate profile ID
        if (profileId <= 0)
        {
            Logger.LogWarning("Invalid profile ID {ProfileId} provided by user {Username}", profileId, GetCurrentUsername());
            return BadRequest(new { detail = "Invalid profile ID" });
        }

        var deleted = await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var profile = await context.ResizeProfiles.FindAsync(profileId);
            if (profile == null)
                return false;

            context.ResizeProfiles.Remove(profile);
            await context.SaveChangesAsync();
            return true;
        });

        if (!deleted)
        {
            Logger.LogWarning("Profile {ProfileId} not found for deletion by user {Username}", profileId, GetCurrentUsername());
            return CreateErrorResponse(404, "Profile not found");
        }
        
        Logger.LogInformation("Profile {ProfileId} deleted by user {Username}", profileId, GetCurrentUsername());
        return CreateSuccessResponse(new { success = true });
    }

}

