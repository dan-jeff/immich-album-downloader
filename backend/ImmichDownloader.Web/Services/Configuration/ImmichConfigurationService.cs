using ImmichDownloader.Web.Services.Database;

namespace ImmichDownloader.Web.Services.Configuration;

public class ImmichConfigurationService : IImmichConfigurationService
{
    private readonly IConfigurationService _configurationService;
    private readonly IImmichService _immichService;
    private readonly ILogger<ImmichConfigurationService> _logger;

    public ImmichConfigurationService(
        IConfigurationService configurationService,
        IImmichService immichService,
        ILogger<ImmichConfigurationService> logger)
    {
        _configurationService = configurationService;
        _immichService = immichService;
        _logger = logger;
    }

    public async Task<string?> GetImmichUrlAsync()
    {
        try
        {
            var (url, _) = await _configurationService.GetImmichSettingsAsync();
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Immich URL");
            return null;
        }
    }

    public async Task<string?> GetImmichApiKeyAsync()
    {
        try
        {
            var (_, apiKey) = await _configurationService.GetImmichSettingsAsync();
            return apiKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Immich API key");
            return null;
        }
    }

    public async Task<(string? Url, string? ApiKey)> GetImmichSettingsAsync()
    {
        try
        {
            return await _configurationService.GetImmichSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Immich settings");
            return (null, null);
        }
    }

    public async Task<bool> ValidateImmichConnectionAsync()
    {
        try
        {
            var (url, apiKey) = await GetImmichSettingsAsync();
            
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Immich settings not configured");
                return false;
            }

            // Test the connection by trying to validate
            var (success, message) = await _immichService.ValidateConnectionAsync(url, apiKey);
            
            _logger.LogDebug("Immich connection validation result: {Success}, {Message}", success, message);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate Immich connection");
            return false;
        }
    }

    public async Task SetImmichSettingsAsync(string url, string apiKey)
    {
        try
        {
            await _configurationService.SetImmichSettingsAsync(url, apiKey);
            _logger.LogInformation("Immich settings updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Immich configuration");
            throw;
        }
    }

    public async Task<bool> IsConfiguredAsync()
    {
        try
        {
            var (url, apiKey) = await GetImmichSettingsAsync();
            return !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(apiKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Immich configuration status");
            return false;
        }
    }

    public async Task<string?> GetDownloadDirectoryAsync()
    {
        try
        {
            return await _configurationService.GetSettingAsync("Download:Directory");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving download directory");
            return null;
        }
    }

    public async Task SetDownloadDirectoryAsync(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory cannot be empty", nameof(directory));

            await _configurationService.SetSettingAsync("Download:Directory", directory);
            _logger.LogDebug("Download directory set to: {Directory}", directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting download directory");
            throw;
        }
    }

    public async Task<int> GetMaxConcurrentDownloadsAsync()
    {
        try
        {
            return await _configurationService.GetSettingAsync("Download:MaxConcurrent", 5);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving max concurrent downloads setting");
            return 5; // Default value
        }
    }

    public async Task SetMaxConcurrentDownloadsAsync(int maxConcurrent)
    {
        try
        {
            if (maxConcurrent <= 0 || maxConcurrent > 20)
                throw new ArgumentException("Max concurrent downloads must be between 1 and 20", nameof(maxConcurrent));

            await _configurationService.SetSettingAsync("Download:MaxConcurrent", maxConcurrent);
            _logger.LogDebug("Max concurrent downloads set to: {MaxConcurrent}", maxConcurrent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting max concurrent downloads");
            throw;
        }
    }

    public async Task<int> GetChunkSizeAsync()
    {
        try
        {
            return await _configurationService.GetSettingAsync("Download:ChunkSize", 50);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chunk size setting");
            return 50; // Default value
        }
    }

    public async Task SetChunkSizeAsync(int chunkSize)
    {
        try
        {
            if (chunkSize <= 0 || chunkSize > 1000)
                throw new ArgumentException("Chunk size must be between 1 and 1000", nameof(chunkSize));

            await _configurationService.SetSettingAsync("Download:ChunkSize", chunkSize);
            _logger.LogDebug("Chunk size set to: {ChunkSize}", chunkSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting chunk size");
            throw;
        }
    }

    public async Task<bool> GetUseStreamingModeAsync()
    {
        try
        {
            return await _configurationService.GetSettingAsync("Download:UseStreaming", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving streaming mode setting");
            return true; // Default to streaming mode
        }
    }

    public async Task SetUseStreamingModeAsync(bool useStreaming)
    {
        try
        {
            await _configurationService.SetSettingAsync("Download:UseStreaming", useStreaming);
            _logger.LogDebug("Streaming mode set to: {UseStreaming}", useStreaming);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting streaming mode");
            throw;
        }
    }
}