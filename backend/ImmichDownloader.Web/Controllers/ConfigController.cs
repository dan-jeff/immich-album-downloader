using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Models.Requests;
using ImmichDownloader.Web.Services;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Controller responsible for managing application configuration including
/// Immich server settings and resize profiles.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImmichService _immichService;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigController"/> class.
    /// </summary>
    /// <param name="context">The database context for configuration operations.</param>
    /// <param name="immichService">The Immich service for server connectivity.</param>
    /// <param name="configuration">The application configuration provider.</param>
    public ConfigController(
        ApplicationDbContext context, 
        IImmichService immichService,
        IConfiguration configuration)
    {
        _context = context;
        _immichService = immichService;
        _configuration = configuration;
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
        var immichUrl = await GetSettingAsync("Immich:Url") ?? "";
        var apiKey = await GetSettingAsync("Immich:ApiKey") ?? "";
        var profiles = await _context.ResizeProfiles.OrderByDescending(p => p.CreatedAt).ToListAsync();

        return Ok(new
        {
            immich_url = immichUrl,
            api_key = apiKey,
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
        // Save to database for persistence
        await SetSettingAsync("Immich:Url", config.immich_url);
        await SetSettingAsync("Immich:ApiKey", config.api_key);
        
        // Configure the service directly for immediate use
        _immichService.Configure(config.immich_url, config.api_key);
        return Ok(new { success = true });
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
        Console.WriteLine($"Received URL: '{config.immich_url}', API Key length: {config.api_key.Length}");
        var (success, message) = await _immichService.ValidateConnectionAsync(config.immich_url, config.api_key);
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
        var profile = new ResizeProfile
        {
            Name = request.Name,
            Width = request.Width,
            Height = request.Height,
            IncludeHorizontal = request.IncludeHorizontal,
            IncludeVertical = request.IncludeVertical
        };

        _context.ResizeProfiles.Add(profile);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, id = profile.Id });
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
        var profile = await _context.ResizeProfiles.FindAsync(profileId);
        if (profile == null)
            return NotFound(new { detail = "Profile not found" });

        profile.Name = request.Name;
        profile.Width = request.Width;
        profile.Height = request.Height;
        profile.IncludeHorizontal = request.IncludeHorizontal;
        profile.IncludeVertical = request.IncludeVertical;

        await _context.SaveChangesAsync();
        return Ok(new { success = true });
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
        var profile = await _context.ResizeProfiles.FindAsync(profileId);
        if (profile == null)
            return NotFound(new { detail = "Profile not found" });

        _context.ResizeProfiles.Remove(profile);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>
    /// Retrieves a configuration setting from the database.
    /// </summary>
    /// <param name="key">The setting key to retrieve.</param>
    /// <returns>The setting value if found, otherwise null.</returns>
    private async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    /// <summary>
    /// Sets a configuration setting in the database, creating it if it doesn't exist.
    /// </summary>
    /// <param name="key">The setting key to set.</param>
    /// <param name="value">The setting value to store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SetSettingAsync(string key, string value)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting != null)
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }
}

