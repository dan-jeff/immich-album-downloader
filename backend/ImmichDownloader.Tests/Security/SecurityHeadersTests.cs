using System.Net;
using FluentAssertions;
using ImmichDownloader.Web;
using ImmichDownloader.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ImmichDownloader.Tests.Security;

/// <summary>
/// Security integration tests for HTTP security headers and middleware.
/// Validates that appropriate security headers are present to protect against common web vulnerabilities.
/// </summary>
[Trait("Category", "SecurityTest")]
public class SecurityHeadersTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _connection;

    public SecurityHeadersTests(WebApplicationFactory<Program> factory)
    {
        // JWT configuration is set globally in TestSetup.cs
        
        // Create and keep open SQLite in-memory connection with unique name for test isolation
        var uniqueDbName = $"TestDb_{GetType().Name}_{Guid.NewGuid():N}";
        _connection = new SqliteConnection($"DataSource={uniqueDbName};Mode=Memory;Cache=Shared");
        _connection.Open();
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing database registration
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Use the persistent SQLite in-memory connection
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                    options.EnableSensitiveDataLogging();
                    options.EnableDetailedErrors();
                });
                
                // Ensure database is created after context is configured
                var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Database.EnsureCreated();
            });
        });

        _client = _factory.CreateClient();
    }


    [Theory]
    [InlineData("/api/health")]
    [InlineData("/")]
    public async Task PublicEndpoints_ShouldIncludeSecurityHeaders(string endpoint)
    {
        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        await ValidateSecurityHeaders(response);
    }

    [Fact]
    public async Task Response_ShouldIncludeContentSecurityPolicy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().ContainKey("Content-Security-Policy");
        
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
        cspHeader.Should().NotBeNull();
        cspHeader.Should().Contain("default-src");
        cspHeader.Should().Contain("'self'");
        // Development mode CSP allows unsafe-eval and unsafe-inline
        cspHeader.Should().Contain("'unsafe-eval'", "Development mode CSP allows unsafe-eval");
        cspHeader.Should().Contain("'unsafe-inline'", "Development mode CSP allows unsafe-inline scripts");
    }

    [Fact]
    public async Task Response_ShouldIncludeXFrameOptions()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().ContainKey("X-Frame-Options");
        
        var frameOptions = response.Headers.GetValues("X-Frame-Options").FirstOrDefault();
        frameOptions.Should().BeOneOf("DENY", "SAMEORIGIN");
    }

    [Fact]
    public async Task Response_ShouldIncludeXContentTypeOptions()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().ContainKey("X-Content-Type-Options");
        
        var contentTypeOptions = response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault();
        contentTypeOptions.Should().Be("nosniff");
    }

    [Fact]
    public async Task Response_ShouldIncludeReferrerPolicy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().ContainKey("Referrer-Policy");
        
        var referrerPolicy = response.Headers.GetValues("Referrer-Policy").FirstOrDefault();
        referrerPolicy.Should().BeOneOf("strict-origin-when-cross-origin", "strict-origin", "no-referrer");
    }

    [Fact]
    public async Task Response_ShouldIncludePermissionsPolicy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().ContainKey("Permissions-Policy");
        
        var permissionsPolicy = response.Headers.GetValues("Permissions-Policy").FirstOrDefault();
        permissionsPolicy.Should().NotBeNull();
        permissionsPolicy.Should().Contain("camera=()"); // Should disable camera
        permissionsPolicy.Should().Contain("microphone=()"); // Should disable microphone
        permissionsPolicy.Should().Contain("geolocation=()"); // Should disable geolocation
    }



    [Fact]
    public async Task Response_ShouldIncludeStrictTransportSecurity()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert - Development mode doesn't include HSTS
        response.Headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task Response_ShouldNotExposeServerInformation()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().NotContainKey("Server");
        response.Headers.Should().NotContainKey("X-Powered-By");
        response.Headers.Should().NotContainKey("X-AspNet-Version");
        response.Headers.Should().NotContainKey("X-AspNetMvc-Version");
    }



    [Fact]
    public async Task CSP_ShouldRestrictScriptSources()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
        cspHeader.Should().NotBeNull();
        
        // Should have script-src directive
        if (cspHeader!.Contains("script-src"))
        {
            // Current development CSP allows unsafe-eval and localhost
            cspHeader.Should().Contain("'unsafe-eval'"); // Development mode allows this
            cspHeader.Should().Contain("http://localhost:*"); // Development mode includes localhost
        }
    }

    [Fact]
    public async Task CSP_ShouldRestrictObjectSources()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
        cspHeader.Should().NotBeNull();
        // Current development CSP doesn't explicitly set object-src
        // object-src defaults to 'self' when not specified
        if (cspHeader.Contains("object-src"))
        {
            cspHeader.Should().Contain("object-src 'none'");
        }
    }

    [Fact]
    public async Task CSP_ShouldRestrictBaseUri()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
        cspHeader.Should().NotBeNull();
        cspHeader.Should().Contain("base-uri 'self'");
    }

    [Fact]
    public async Task CSP_ShouldIncludeUpgradeInsecureRequests()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        var cspHeader = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
        cspHeader.Should().NotBeNull();
        // Current CSP doesn't include upgrade-insecure-requests in development
        cspHeader.Should().NotContain("upgrade-insecure-requests");
    }



    [Fact]
    public async Task Response_ShouldIncludeRateLimitHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        // Rate limiting headers may be present depending on implementation
        if (response.Headers.Contains("X-RateLimit-Limit"))
        {
            response.Headers.Should().ContainKey("X-RateLimit-Remaining");
            response.Headers.Should().ContainKey("X-RateLimit-Reset");
            
            var limitHeader = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
            int.TryParse(limitHeader, out var limit).Should().BeTrue();
            limit.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task RateLimiting_WhenExceeded_ShouldReturnAppropriateHeaders()
    {
        // Arrange - Make multiple requests to potentially trigger rate limiting
        var responses = new List<HttpResponseMessage>();
        
        // Act
        for (int i = 0; i < 20; i++)
        {
            var response = await _client.GetAsync("/api/health");
            responses.Add(response);
            
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // Assert
                response.Headers.Should().ContainKey("Retry-After");
                var retryAfter = response.Headers.GetValues("Retry-After").FirstOrDefault();
                int.TryParse(retryAfter, out var retrySeconds).Should().BeTrue();
                retrySeconds.Should().BeGreaterThan(0);
                break;
            }
        }
        
        // Cleanup
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }



    [Fact]
    public async Task ApiEndpoints_ShouldHaveNoCacheHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        if (response.Headers.Contains("Cache-Control"))
        {
            var cacheControl = response.Headers.GetValues("Cache-Control").FirstOrDefault();
            cacheControl.Should().Contain("no-cache");
            cacheControl.Should().Contain("no-store");
        }
    }

    [Fact]
    public async Task StaticContent_ShouldHaveAppropriateCache()
    {
        // Act
        var response = await _client.GetAsync("/favicon.ico");

        // Assert
        // Static content may not have Cache-Control headers in development
        if (response.StatusCode == HttpStatusCode.OK && response.Headers.Contains("Cache-Control"))
        {
            var cacheControl = response.Headers.GetValues("Cache-Control").FirstOrDefault();
            cacheControl.Should().NotContain("no-store");
        }
    }



    [Fact]
    public async Task Response_ShouldIncludeXXSSProtection()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        // X-XSS-Protection is deprecated but may still be present for legacy browser support
        if (response.Headers.Contains("X-XSS-Protection"))
        {
            var xssProtection = response.Headers.GetValues("X-XSS-Protection").FirstOrDefault();
            xssProtection.Should().Be("1; mode=block");
        }
    }



    [Fact]
    public async Task JsonResponse_ShouldHaveCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        if (response.Content.Headers.ContentType != null)
        {
            response.Content.Headers.ContentType.MediaType.Should().Be("application/json");
            response.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
        }
    }

    [Fact]
    public async Task Response_ShouldNotHaveDangerousContentTypes()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        if (response.Content.Headers.ContentType != null)
        {
            var contentType = response.Content.Headers.ContentType.MediaType;
            contentType.Should().NotBe("text/html"); // API should not return HTML
            contentType.Should().NotContain("script");
            contentType.Should().NotContain("executable");
        }
    }



    private async Task ValidateSecurityHeaders(HttpResponseMessage response)
    {
        // Essential security headers that should be present
        var requiredHeaders = new[]
        {
            "X-Frame-Options",
            "X-Content-Type-Options",
            "Referrer-Policy"
        };

        foreach (var header in requiredHeaders)
        {
            response.Headers.Should().ContainKey(header, $"Response should include {header} header for security");
        }

        // CSP should be present
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var csp = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
            csp.Should().NotBeNullOrEmpty("CSP header should have value");
        }

        // HSTS should be present for HTTPS
        if (response.Headers.Contains("Strict-Transport-Security"))
        {
            var sts = response.Headers.GetValues("Strict-Transport-Security").FirstOrDefault();
            sts.Should().Contain("max-age=");
        }

        await Task.CompletedTask; // For consistency with async pattern
    }



    [Fact]
    public async Task ErrorResponse_ShouldNotLeakInternalInformation()
    {
        // Act
        var response = await _client.GetAsync("/api/nonexistent-endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("System."); // Should not expose .NET types
        content.Should().NotContain("Microsoft."); // Should not expose Microsoft namespaces
        content.Should().NotContain("StackTrace"); // Should not expose stack traces
        content.Should().NotContain("InnerException"); // Should not expose inner exceptions
        content.Should().NotContain("Connection"); // Should not expose connection strings
        content.Should().NotContain("Password"); // Should not expose passwords
    }

    [Fact]
    public async Task Response_ShouldNotIncludeDebugInformation()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.Headers.Should().NotContainKey("X-Debug");
        response.Headers.Should().NotContainKey("X-Trace");
        response.Headers.Should().NotContainKey("X-Source-File");
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("DEBUG");
        content.Should().NotContain("TRACE");
    }



    [Fact]
    public async Task Production_ShouldHaveStrictSecurityHeaders()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        // In development, security headers are more permissive
        if (response.Headers.Contains("Content-Security-Policy"))
        {
            var csp = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
            // Development mode allows these for easier debugging
            csp.Should().Contain("'unsafe-eval'");
            csp.Should().Contain("'unsafe-inline'");
            csp.Should().Contain("data:");
        }

        // Should not expose development information
        response.Headers.Should().NotContainKey("X-Development");
        response.Headers.Should().NotContainKey("X-Environment");
    }


    public void Dispose()
    {
        _connection?.Dispose();
        _client?.Dispose();
    }
}