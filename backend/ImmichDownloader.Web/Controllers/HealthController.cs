using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Services;
using System.Diagnostics;
using System.Reflection;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Health check endpoints for monitoring application status and dependencies.
/// Provides various levels of health information for different monitoring needs.
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IImmichService _immichService;
    private readonly ILogger<HealthController> _logger;

    /// <summary>
    /// Initializes a new instance of the HealthController.
    /// </summary>
    /// <param name="context">Database context for database health checks.</param>
    /// <param name="immichService">Immich service for external dependency checks.</param>
    /// <param name="logger">Logger for health check operations.</param>
    public HealthController(
        ApplicationDbContext context,
        IImmichService immichService,
        ILogger<HealthController> logger)
    {
        _context = context;
        _immichService = immichService;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint that returns a simple "healthy" status.
    /// Used for basic uptime monitoring and load balancer health checks.
    /// </summary>
    /// <returns>Simple health status.</returns>
    /// <response code="200">Service is healthy.</response>
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Detailed health check that includes database connectivity and basic system information.
    /// Provides more comprehensive health information for monitoring systems.
    /// </summary>
    /// <returns>Detailed health status including dependencies.</returns>
    /// <response code="200">Service and dependencies are healthy.</response>
    /// <response code="503">Service or dependencies are unhealthy.</response>
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailedHealth()
    {
        var healthChecks = new List<HealthCheckResult>();
        var overallStatus = "healthy";

        // Check database connectivity
        var dbHealth = await CheckDatabaseHealth();
        healthChecks.Add(dbHealth);
        if (dbHealth.Status != "healthy")
            overallStatus = "unhealthy";

        // Check application metrics
        var appHealth = CheckApplicationHealth();
        healthChecks.Add(appHealth);

        // Check disk space
        var diskHealth = CheckDiskHealth();
        healthChecks.Add(diskHealth);
        if (diskHealth.Status != "healthy")
            overallStatus = "degraded";

        // Check Immich connectivity
        var immichHealth = await CheckImmichHealth();
        healthChecks.Add(immichHealth);
        if (immichHealth.Status == "unhealthy")
            overallStatus = "degraded";

        var result = new
        {
            status = overallStatus,
            timestamp = DateTime.UtcNow,
            version = GetApplicationVersion(),
            uptime = GetUptime(),
            checks = healthChecks
        };

        var statusCode = overallStatus switch
        {
            "healthy" => 200,
            "degraded" => 200,
            _ => 503
        };

        return StatusCode(statusCode, result);
    }

    /// <summary>
    /// Readiness check endpoint that verifies the application is ready to handle requests.
    /// Checks all critical dependencies required for normal operation.
    /// </summary>
    /// <returns>Readiness status.</returns>
    /// <response code="200">Service is ready to handle requests.</response>
    /// <response code="503">Service is not ready.</response>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            // Check database is accessible and has required tables
            var canConnectToDb = await _context.Database.CanConnectAsync();
            if (!canConnectToDb)
            {
                _logger.LogWarning("Readiness check failed: Cannot connect to database");
                return StatusCode(503, new { status = "not_ready", reason = "database_unavailable" });
            }

            // Check if database is migrated
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogWarning("Readiness check failed: Database migrations pending");
                return StatusCode(503, new { status = "not_ready", reason = "migrations_pending" });
            }

            // Check if required directories exist
            var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "data");
            if (!Directory.Exists(dataPath))
            {
                _logger.LogWarning("Readiness check failed: Data directory does not exist");
                return StatusCode(503, new { status = "not_ready", reason = "data_directory_missing" });
            }

            return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed with exception");
            return StatusCode(503, new { status = "not_ready", reason = "exception", error = ex.Message });
        }
    }

    /// <summary>
    /// Liveness check endpoint that verifies the application is running and responsive.
    /// Used by orchestrators to determine if the application needs to be restarted.
    /// </summary>
    /// <returns>Liveness status.</returns>
    /// <response code="200">Service is alive and responsive.</response>
    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        // Simple check that the application is responsive
        // This endpoint should always return 200 unless the application is completely broken
        return Ok(new 
        { 
            status = "alive", 
            timestamp = DateTime.UtcNow,
            process_id = Environment.ProcessId,
            thread_count = Process.GetCurrentProcess().Threads.Count
        });
    }

    /// <summary>
    /// Checks database health and connectivity.
    /// </summary>
    private async Task<HealthCheckResult> CheckDatabaseHealth()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var canConnect = await _context.Database.CanConnectAsync();
            stopwatch.Stop();

            if (canConnect)
            {
                // Test a simple query
                var userCount = await _context.Users.CountAsync();
                
                return new HealthCheckResult
                {
                    Name = "database",
                    Status = "healthy",
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Details = new { user_count = userCount, connection_time_ms = stopwatch.ElapsedMilliseconds }
                };
            }
            else
            {
                return new HealthCheckResult
                {
                    Name = "database",
                    Status = "unhealthy",
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Details = new { error = "Cannot connect to database" }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new HealthCheckResult
            {
                Name = "database",
                Status = "unhealthy",
                ResponseTime = null,
                Details = new { error = ex.Message }
            };
        }
    }

    /// <summary>
    /// Checks general application health metrics.
    /// </summary>
    private static HealthCheckResult CheckApplicationHealth()
    {
        var process = Process.GetCurrentProcess();
        
        return new HealthCheckResult
        {
            Name = "application",
            Status = "healthy",
            ResponseTime = null,
            Details = new
            {
                memory_usage_mb = process.WorkingSet64 / 1024 / 1024,
                cpu_time_ms = process.TotalProcessorTime.TotalMilliseconds,
                thread_count = process.Threads.Count,
                gc_total_memory_mb = GC.GetTotalMemory(false) / 1024 / 1024
            }
        };
    }

    /// <summary>
    /// Checks available disk space.
    /// </summary>
    private HealthCheckResult CheckDiskHealth()
    {
        try
        {
            var drive = new DriveInfo(Directory.GetCurrentDirectory());
            var freeSpaceGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
            var totalSpaceGB = drive.TotalSize / (1024 * 1024 * 1024);
            var usagePercentage = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;

            var status = usagePercentage > 90 ? "unhealthy" : 
                        usagePercentage > 80 ? "degraded" : "healthy";

            return new HealthCheckResult
            {
                Name = "disk_space",
                Status = status,
                ResponseTime = null,
                Details = new
                {
                    free_space_gb = freeSpaceGB,
                    total_space_gb = totalSpaceGB,
                    usage_percentage = Math.Round(usagePercentage, 2)
                }
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Name = "disk_space",
                Status = "unhealthy",
                ResponseTime = null,
                Details = new { error = ex.Message }
            };
        }
    }

    /// <summary>
    /// Checks Immich server connectivity if configured.
    /// </summary>
    private async Task<HealthCheckResult> CheckImmichHealth()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Get Immich settings from database
            var urlSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Immich:Url");
            var apiKeySetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Immich:ApiKey");
            
            if (string.IsNullOrEmpty(urlSetting?.Value) || string.IsNullOrEmpty(apiKeySetting?.Value))
            {
                stopwatch.Stop();
                return new HealthCheckResult
                {
                    Name = "immich_server",
                    Status = "not_configured",
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    Details = new { message = "Immich server not configured" }
                };
            }

            // Test connection
            _immichService.Configure(urlSetting.Value, apiKeySetting.Value);
            var (success, message) = await _immichService.ValidateConnectionAsync(urlSetting.Value, apiKeySetting.Value);
            stopwatch.Stop();

            return new HealthCheckResult
            {
                Name = "immich_server",
                Status = success ? "healthy" : "unhealthy",
                ResponseTime = stopwatch.ElapsedMilliseconds,
                Details = new 
                { 
                    message = message,
                    server_url = urlSetting.Value,
                    connection_time_ms = stopwatch.ElapsedMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Immich health check failed");
            return new HealthCheckResult
            {
                Name = "immich_server",
                Status = "unhealthy",
                ResponseTime = null,
                Details = new { error = ex.Message }
            };
        }
    }

    /// <summary>
    /// Gets the application version from assembly information.
    /// </summary>
    private static string GetApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Gets the application uptime.
    /// </summary>
    private static string GetUptime()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.Now - process.StartTime;
        return uptime.ToString(@"dd\.hh\:mm\:ss");
    }

    /// <summary>
    /// Represents a health check result.
    /// </summary>
    private class HealthCheckResult
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long? ResponseTime { get; set; }
        public object? Details { get; set; }
    }
}