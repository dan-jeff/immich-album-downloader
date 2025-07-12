namespace ImmichDownloader.Web.Services.Configuration;

public interface IImmichConfigurationService
{
    Task<string?> GetImmichUrlAsync();
    Task<string?> GetImmichApiKeyAsync();
    Task<(string? Url, string? ApiKey)> GetImmichSettingsAsync();
    Task<bool> ValidateImmichConnectionAsync();
    Task SetImmichSettingsAsync(string url, string apiKey);
    Task<bool> IsConfiguredAsync();
    
    Task<string?> GetDownloadDirectoryAsync();
    Task SetDownloadDirectoryAsync(string directory);
    
    Task<int> GetMaxConcurrentDownloadsAsync();
    Task SetMaxConcurrentDownloadsAsync(int maxConcurrent);
    
    Task<int> GetChunkSizeAsync();
    Task SetChunkSizeAsync(int chunkSize);
    
    Task<bool> GetUseStreamingModeAsync();
    Task SetUseStreamingModeAsync(bool useStreaming);
}