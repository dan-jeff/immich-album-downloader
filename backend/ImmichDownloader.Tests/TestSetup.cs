using System.Runtime.CompilerServices;

namespace ImmichDownloader.Tests;

/// <summary>
/// Global test setup that runs before any tests execute.
/// This is needed to disable file watching before WebApplication.CreateBuilder runs.
/// </summary>
public static class TestSetup
{
    /// <summary>
    /// Module initializer that runs automatically when the test assembly loads.
    /// This sets up environment variables needed for all tests.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        // Disable file watching to prevent inotify limit issues in tests
        Environment.SetEnvironmentVariable("DOTNET_DISABLE_FILE_WATCHING", "true");
        
        // Set default test JWT configuration
        Environment.SetEnvironmentVariable("JWT_SECRET_KEY", "test-jwt-key-for-component-testing-shared-across-all-tests");
        Environment.SetEnvironmentVariable("JWT_SKIP_VALIDATION", "true");
        
        // Set test environment
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }
}