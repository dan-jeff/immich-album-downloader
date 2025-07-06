using System.Collections.Concurrent;
using System.Net;

namespace ImmichDownloader.Web.Middleware;

/// <summary>
/// Middleware that implements rate limiting to protect against abuse and DoS attacks.
/// Uses a sliding window approach with different limits for different endpoint types.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly bool _isEnabled;
    
    // Rate limiting stores (IP -> endpoint -> request times)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<DateTime>>> _requestHistory;
    private readonly Timer _cleanupTimer;

    // Rate limiting rules
    private readonly Dictionary<string, RateLimitRule> _rateLimitRules;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _isEnabled = configuration.GetValue<bool>("ENABLE_RATE_LIMITING", true);
        
        _requestHistory = new ConcurrentDictionary<string, ConcurrentDictionary<string, List<DateTime>>>();
        
        // Initialize rate limiting rules
        _rateLimitRules = InitializeRateLimitRules();
        
        // Cleanup timer runs every 5 minutes to remove old entries
        _cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        
        _logger.LogInformation("Rate limiting middleware initialized with {RuleCount} rules. Enabled: {Enabled}", 
            _rateLimitRules.Count, _isEnabled);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_isEnabled)
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        var endpoint = GetEndpointKey(context);
        
        if (await IsRateLimited(clientIp, endpoint, context))
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("Retry-After", "60");
            
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            
            _logger.LogWarning("Rate limit exceeded for IP {ClientIp} on endpoint {Endpoint}", 
                clientIp, endpoint);
            return;
        }

        // Record the request
        RecordRequest(clientIp, endpoint);

        await _next(context);
    }

    private Dictionary<string, RateLimitRule> InitializeRateLimitRules()
    {
        return new Dictionary<string, RateLimitRule>
        {
            // Authentication endpoints - stricter limits
            ["auth"] = new RateLimitRule
            {
                MaxRequests = _configuration.GetValue<int>("AUTH_RATE_LIMIT", 5),
                WindowMinutes = _configuration.GetValue<int>("AUTH_RATE_LIMIT_WINDOW_MINUTES", 1),
                Description = "Authentication endpoints"
            },
            
            // API endpoints - moderate limits
            ["api"] = new RateLimitRule
            {
                MaxRequests = _configuration.GetValue<int>("API_RATE_LIMIT", 100),
                WindowMinutes = _configuration.GetValue<int>("API_RATE_LIMIT_WINDOW_MINUTES", 1),
                Description = "General API endpoints"
            },
            
            // Download endpoints - more restrictive due to resource usage
            ["download"] = new RateLimitRule
            {
                MaxRequests = _configuration.GetValue<int>("DOWNLOAD_RATE_LIMIT", 10),
                WindowMinutes = _configuration.GetValue<int>("DOWNLOAD_RATE_LIMIT_WINDOW_MINUTES", 5),
                Description = "Download endpoints"
            },
            
            // File upload/processing endpoints
            ["upload"] = new RateLimitRule
            {
                MaxRequests = _configuration.GetValue<int>("UPLOAD_RATE_LIMIT", 20),
                WindowMinutes = _configuration.GetValue<int>("UPLOAD_RATE_LIMIT_WINDOW_MINUTES", 1),
                Description = "Upload/processing endpoints"
            },
            
            // Default for all other endpoints
            ["default"] = new RateLimitRule
            {
                MaxRequests = _configuration.GetValue<int>("DEFAULT_RATE_LIMIT", 60),
                WindowMinutes = _configuration.GetValue<int>("DEFAULT_RATE_LIMIT_WINDOW_MINUTES", 1),
                Description = "Default rate limit"
            }
        };
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Try to get the real IP from various headers (for proxy scenarios)
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                       ?? context.Request.Headers["X-Real-IP"].FirstOrDefault()
                       ?? context.Request.Headers["CF-Connecting-IP"].FirstOrDefault() // Cloudflare
                       ?? context.Connection.RemoteIpAddress?.ToString()
                       ?? "unknown";

        // Validate IP address format
        if (IPAddress.TryParse(ipAddress, out _))
        {
            return ipAddress;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string GetEndpointKey(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Categorize endpoints for different rate limits
        if (path.StartsWith("/api/auth"))
            return "auth";
        if (path.StartsWith("/api/downloads") || path.StartsWith("/api/download"))
            return "download";
        if (path.StartsWith("/api/resize") || path.Contains("upload"))
            return "upload";
        if (path.StartsWith("/api"))
            return "api";

        return "default";
    }

    private async Task<bool> IsRateLimited(string clientIp, string endpoint, HttpContext context)
    {
        if (!_rateLimitRules.TryGetValue(endpoint, out var rule))
        {
            rule = _rateLimitRules["default"];
        }

        var clientHistory = _requestHistory.GetOrAdd(clientIp, _ => new ConcurrentDictionary<string, List<DateTime>>());
        var endpointHistory = clientHistory.GetOrAdd(endpoint, _ => new List<DateTime>());

        lock (endpointHistory)
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-rule.WindowMinutes);
            
            // Remove old requests outside the window
            endpointHistory.RemoveAll(time => time < windowStart);
            
            // Check if we're over the limit
            if (endpointHistory.Count >= rule.MaxRequests)
            {
                return true;
            }
        }

        return false;
    }

    private void RecordRequest(string clientIp, string endpoint)
    {
        var clientHistory = _requestHistory.GetOrAdd(clientIp, _ => new ConcurrentDictionary<string, List<DateTime>>());
        var endpointHistory = clientHistory.GetOrAdd(endpoint, _ => new List<DateTime>());

        lock (endpointHistory)
        {
            endpointHistory.Add(DateTime.UtcNow);
        }
    }

    private void CleanupOldEntries(object? state)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-1); // Keep last hour of data
            var clientsToRemove = new List<string>();

            foreach (var clientKvp in _requestHistory)
            {
                var endpointsToRemove = new List<string>();
                
                foreach (var endpointKvp in clientKvp.Value)
                {
                    lock (endpointKvp.Value)
                    {
                        endpointKvp.Value.RemoveAll(time => time < cutoffTime);
                        
                        if (endpointKvp.Value.Count == 0)
                        {
                            endpointsToRemove.Add(endpointKvp.Key);
                        }
                    }
                }

                // Remove empty endpoint histories
                foreach (var endpoint in endpointsToRemove)
                {
                    clientKvp.Value.TryRemove(endpoint, out _);
                }

                // Mark client for removal if no endpoints remain
                if (clientKvp.Value.IsEmpty)
                {
                    clientsToRemove.Add(clientKvp.Key);
                }
            }

            // Remove empty client histories
            foreach (var client in clientsToRemove)
            {
                _requestHistory.TryRemove(client, out _);
            }

            _logger.LogDebug("Rate limiting cleanup completed. Active clients: {ClientCount}", 
                _requestHistory.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limiting cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

/// <summary>
/// Represents a rate limiting rule with request limits and time windows.
/// </summary>
public class RateLimitRule
{
    public int MaxRequests { get; set; }
    public int WindowMinutes { get; set; }
    public string Description { get; set; } = string.Empty;
}