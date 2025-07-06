namespace ImmichDownloader.Web.Middleware;

/// <summary>
/// Middleware that adds security headers to HTTP responses to protect against common web vulnerabilities.
/// Implements OWASP recommended security headers for defense in depth.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;

        // X-Content-Type-Options: Prevent MIME type sniffing
        if (!response.Headers.ContainsKey("X-Content-Type-Options"))
        {
            response.Headers.Append("X-Content-Type-Options", "nosniff");
        }

        // X-Frame-Options: Prevent clickjacking attacks
        if (!response.Headers.ContainsKey("X-Frame-Options"))
        {
            response.Headers.Append("X-Frame-Options", "DENY");
        }

        // X-XSS-Protection: Enable XSS filtering (legacy browsers)
        if (!response.Headers.ContainsKey("X-XSS-Protection"))
        {
            response.Headers.Append("X-XSS-Protection", "1; mode=block");
        }

        // Referrer-Policy: Control referrer information
        if (!response.Headers.ContainsKey("Referrer-Policy"))
        {
            response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        }

        // Content-Security-Policy: Prevent XSS and data injection attacks
        if (!response.Headers.ContainsKey("Content-Security-Policy"))
        {
            var csp = BuildContentSecurityPolicy();
            response.Headers.Append("Content-Security-Policy", csp);
        }

        // Strict-Transport-Security: Enforce HTTPS (production only)
        if (!_isDevelopment && !response.Headers.ContainsKey("Strict-Transport-Security"))
        {
            response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
        }

        // Permissions-Policy: Control browser features
        if (!response.Headers.ContainsKey("Permissions-Policy"))
        {
            response.Headers.Append("Permissions-Policy", 
                "camera=(), microphone=(), geolocation=(), payment=(), usb=()");
        }

        // Remove server information disclosure
        response.Headers.Remove("Server");
        response.Headers.Remove("X-Powered-By");
        response.Headers.Remove("X-AspNet-Version");
        response.Headers.Remove("X-AspNetMvc-Version");

        // Add security information header for monitoring
        if (!response.Headers.ContainsKey("X-Security-Policy"))
        {
            response.Headers.Append("X-Security-Policy", "enhanced");
        }

        _logger.LogDebug("Security headers applied to {Path}", context.Request.Path);
    }

    private string BuildContentSecurityPolicy()
    {
        if (_isDevelopment)
        {
            // More permissive CSP for development
            return "default-src 'self'; " +
                   "script-src 'self' 'unsafe-inline' 'unsafe-eval' http://localhost:* ws://localhost:*; " +
                   "style-src 'self' 'unsafe-inline'; " +
                   "img-src 'self' data: blob:; " +
                   "font-src 'self' data:; " +
                   "connect-src 'self' http://localhost:* ws://localhost:* wss://localhost:*; " +
                   "frame-ancestors 'none'; " +
                   "base-uri 'self'; " +
                   "form-action 'self'";
        }
        else
        {
            // Strict CSP for production
            return "default-src 'self'; " +
                   "script-src 'self'; " +
                   "style-src 'self' 'unsafe-inline'; " +
                   "img-src 'self' data: blob:; " +
                   "font-src 'self'; " +
                   "connect-src 'self' wss:; " +
                   "frame-ancestors 'none'; " +
                   "base-uri 'self'; " +
                   "form-action 'self'; " +
                   "upgrade-insecure-requests";
        }
    }
}