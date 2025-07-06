using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ImmichDownloader.Web.Services.Database;

/// <summary>
/// Implementation of centralized configuration service that manages application settings
/// stored in the database with type-safe access and caching capabilities.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IDatabaseService _databaseService;
    private readonly ILogger<ConfigurationService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ConfigurationService(IDatabaseService databaseService, ILogger<ConfigurationService> logger)
    {
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(string? Url, string? ApiKey)> GetImmichSettingsAsync()
    {
        _logger.LogDebug("Retrieving Immich settings");
        
        var settings = await GetSettingsAsync("Immich:Url", "Immich:ApiKey");
        
        settings.TryGetValue("Immich:Url", out var url);
        settings.TryGetValue("Immich:ApiKey", out var apiKey);
        
        _logger.LogDebug("Retrieved Immich settings: URL={HasUrl}, ApiKey={HasApiKey}", 
            !string.IsNullOrEmpty(url), !string.IsNullOrEmpty(apiKey));
        
        return (url, apiKey);
    }

    /// <inheritdoc />
    public async Task SetImmichSettingsAsync(string url, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be empty", nameof(apiKey));

        _logger.LogInformation("Updating Immich settings");
        
        var settings = new Dictionary<string, string>
        {
            ["Immich:Url"] = url.TrimEnd('/'),
            ["Immich:ApiKey"] = apiKey
        };

        await SetSettingsAsync(settings);
        _logger.LogInformation("Immich settings updated successfully");
    }

    /// <inheritdoc />
    public async Task<T> GetSettingAsync<T>(string key, T defaultValue = default!)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        try
        {
            var value = await GetSettingAsync(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            // Handle simple types
            if (typeof(T) == typeof(string))
                return (T)(object)value;
            if (typeof(T) == typeof(int) && int.TryParse(value, out var intValue))
                return (T)(object)intValue;
            if (typeof(T) == typeof(bool) && bool.TryParse(value, out var boolValue))
                return (T)(object)boolValue;
            if (typeof(T) == typeof(double) && double.TryParse(value, out var doubleValue))
                return (T)(object)doubleValue;

            // Handle complex types via JSON
            return JsonSerializer.Deserialize<T>(value, JsonOptions) ?? defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize setting {Key} to type {Type}, returning default value", key, typeof(T).Name);
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public async Task SetSettingAsync<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        string serializedValue;
        
        // Handle simple types
        if (value is string stringValue)
            serializedValue = stringValue;
        else if (value is int or bool or double or float or decimal)
            serializedValue = value.ToString()!;
        else
            serializedValue = JsonSerializer.Serialize(value, JsonOptions);

        await SetSettingAsync(key, serializedValue);
    }

    /// <inheritdoc />
    public async Task<string?> GetSettingAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            var setting = await context.AppSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key);
            
            return setting?.Value;
        });
    }

    /// <inheritdoc />
    public async Task SetSettingAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var existing = await context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                _logger.LogDebug("Updated setting {Key}", key);
            }
            else
            {
                context.AppSettings.Add(new AppSetting
                {
                    Key = key,
                    Value = value,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                _logger.LogDebug("Created new setting {Key}", key);
            }

            await context.SaveChangesAsync();
        });
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetSettingsAsync(params string[] keys)
    {
        if (keys == null || keys.Length == 0)
            return new Dictionary<string, string>();

        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            var settings = await context.AppSettings
                .AsNoTracking()
                .Where(s => keys.Contains(s.Key))
                .Select(s => new { s.Key, s.Value })
                .ToListAsync();

            return settings.ToDictionary(s => s.Key, s => s.Value);
        });
    }

    /// <inheritdoc />
    public async Task SetSettingsAsync(Dictionary<string, string> settings)
    {
        if (settings == null || settings.Count == 0)
            return;

        await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var keys = settings.Keys.ToArray();
            var existingSettings = await context.AppSettings
                .Where(s => keys.Contains(s.Key))
                .ToListAsync();

            var now = DateTime.UtcNow;

            foreach (var kvp in settings)
            {
                var existing = existingSettings.FirstOrDefault(s => s.Key == kvp.Key);
                if (existing != null)
                {
                    existing.Value = kvp.Value;
                    existing.UpdatedAt = now;
                }
                else
                {
                    context.AppSettings.Add(new AppSetting
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            await context.SaveChangesAsync();
            _logger.LogDebug("Updated {Count} settings", settings.Count);
        });
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSettingAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        return await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var setting = await context.AppSettings
                .FirstOrDefaultAsync(s => s.Key == key);

            if (setting == null)
                return false;

            context.AppSettings.Remove(setting);
            await context.SaveChangesAsync();
            
            _logger.LogDebug("Deleted setting {Key}", key);
            return true;
        });
    }

    /// <inheritdoc />
    public async Task<bool> SettingExistsAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            return await context.AppSettings
                .AsNoTracking()
                .AnyAsync(s => s.Key == key);
        });
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        return await _databaseService.ExecuteWithScopeAsync(async context =>
        {
            var settings = await context.AppSettings
                .AsNoTracking()
                .Select(s => new { s.Key, s.Value })
                .ToListAsync();

            return settings.ToDictionary(s => s.Key, s => s.Value);
        });
    }

    /// <inheritdoc />
    public async Task ClearAllSettingsAsync()
    {
        await _databaseService.ExecuteInTransactionAsync(async context =>
        {
            var allSettings = await context.AppSettings.ToListAsync();
            context.AppSettings.RemoveRange(allSettings);
            await context.SaveChangesAsync();
            
            _logger.LogWarning("Cleared all application settings ({Count} settings removed)", allSettings.Count);
        });
    }
}