using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ImmichDownloader.Web.Middleware;

/// <summary>
/// Global exception handling middleware that provides consistent error responses
/// and proper logging for all unhandled exceptions in the application.
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the GlobalExceptionHandlingMiddleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger for exception logging.</param>
    /// <param name="environment">Host environment for determining error detail level.</param>
    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Processes the HTTP request and handles any unhandled exceptions.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Handles exceptions by logging them and returning appropriate error responses.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="exception">The exception that occurred.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Log the exception with full details
        _logger.LogError(exception, "An unhandled exception occurred during request {Method} {Path}. User: {User}, IP: {RemoteIP}", 
            context.Request.Method, 
            context.Request.Path,
            context.User?.Identity?.Name ?? "Anonymous",
            context.Connection.RemoteIpAddress?.ToString() ?? "Unknown");

        // Determine response based on exception type
        var response = exception switch
        {
            ArgumentException argEx => CreateErrorResponse(
                HttpStatusCode.BadRequest,
                "Invalid Request",
                argEx.Message,
                "INVALID_ARGUMENT"),

            UnauthorizedAccessException => CreateErrorResponse(
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "Access denied",
                "UNAUTHORIZED"),

            FileNotFoundException => CreateErrorResponse(
                HttpStatusCode.NotFound,
                "Resource Not Found",
                "The requested resource was not found",
                "RESOURCE_NOT_FOUND"),

            InvalidOperationException invalidOpEx => CreateErrorResponse(
                HttpStatusCode.BadRequest,
                "Invalid Operation",
                invalidOpEx.Message,
                "INVALID_OPERATION"),

            TaskCanceledException => CreateErrorResponse(
                HttpStatusCode.RequestTimeout,
                "Request Timeout",
                "The request was cancelled or timed out",
                "REQUEST_TIMEOUT"),

            _ => CreateErrorResponse(
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred",
                "INTERNAL_ERROR")
        };

        // Set response headers
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = "application/json";

        // Add security headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Serialize and return error response
        var jsonResponse = JsonSerializer.Serialize(response.Body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="title">The error title.</param>
    /// <param name="detail">The error detail message.</param>
    /// <param name="errorCode">A specific error code for client handling.</param>
    /// <returns>An error response object.</returns>
    private static ErrorResponse CreateErrorResponse(
        HttpStatusCode statusCode, 
        string title, 
        string detail, 
        string errorCode)
    {
        return new ErrorResponse
        {
            StatusCode = statusCode,
            Body = new ProblemDetails
            {
                Type = "https://httpstatuses.com/" + (int)statusCode,
                Title = title,
                Detail = detail,
                Status = (int)statusCode,
                Extensions = new Dictionary<string, object?>
                {
                    ["errorCode"] = errorCode,
                    ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            }
        };
    }

    /// <summary>
    /// Represents an error response with status code and body.
    /// </summary>
    private record ErrorResponse
    {
        public HttpStatusCode StatusCode { get; init; }
        public ProblemDetails Body { get; init; } = null!;
    }
}