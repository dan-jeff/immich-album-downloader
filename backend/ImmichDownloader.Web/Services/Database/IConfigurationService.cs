using ImmichDownloader.Web.Models;

namespace ImmichDownloader.Web.Services.Database;

/// <summary>
/// Provides centralized configuration management for application settings.
/// Abstracts database-backed configuration storage and provides type-safe access to settings.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets Immich server connection settings.
    /// </summary>
    /// <returns>A tuple containing the Immich URL and API key, or null values if not configured.</returns>
    Task<(string? Url, string? ApiKey)> GetImmichSettingsAsync();

    /// <summary>
    /// Sets Immich server connection settings.
    /// </summary>
    /// <param name="url">The Immich server URL.</param>
    /// <param name="apiKey">The Immich API key.</param>
    Task SetImmichSettingsAsync(string url, string apiKey);

    /// <summary>
    /// Gets a typed configuration value by key.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the setting value to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="defaultValue">The default value to return if the setting doesn't exist.</param>
    /// <returns>The configuration value or the default value.</returns>
    Task<T> GetSettingAsync<T>(string key, T defaultValue = default!);

    /// <summary>
    /// Sets a typed configuration value.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to store.</param>
    Task SetSettingAsync<T>(string key, T value);

    /// <summary>
    /// Gets a string configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value or null if not found.</returns>
    Task<string?> GetSettingAsync(string key);

    /// <summary>
    /// Sets a string configuration value.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The value to store.</param>
    Task SetSettingAsync(string key, string value);

    /// <summary>
    /// Gets multiple configuration values by their keys.
    /// </summary>
    /// <param name="keys">The configuration keys to retrieve.</param>
    /// <returns>A dictionary containing key-value pairs for existing settings.</returns>
    Task<Dictionary<string, string>> GetSettingsAsync(params string[] keys);

    /// <summary>
    /// Sets multiple configuration values in a single transaction.
    /// </summary>
    /// <param name="settings">Dictionary of key-value pairs to store.</param>
    Task SetSettingsAsync(Dictionary<string, string> settings);

    /// <summary>
    /// Deletes a configuration setting.
    /// </summary>
    /// <param name="key">The configuration key to delete.</param>
    /// <returns>True if the setting was deleted, false if it didn't exist.</returns>
    Task<bool> DeleteSettingAsync(string key);

    /// <summary>
    /// Checks if a configuration setting exists.
    /// </summary>
    /// <param name="key">The configuration key to check.</param>
    /// <returns>True if the setting exists, false otherwise.</returns>
    Task<bool> SettingExistsAsync(string key);

    /// <summary>
    /// Gets all configuration settings.
    /// </summary>
    /// <returns>Dictionary containing all configuration key-value pairs.</returns>
    Task<Dictionary<string, string>> GetAllSettingsAsync();

    /// <summary>
    /// Clears all configuration settings.
    /// </summary>
    Task ClearAllSettingsAsync();
}