using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Services;
using ImmichDownloader.Web.Services.Database;
using ImmichDownloader.Web.Hubs;
using ImmichDownloader.Web.Middleware;
using ImmichDownloader.Web.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SixLabors.ImageSharp;

var builder = WebApplication.CreateBuilder(args);

// Disable file watching in test environment to prevent inotify limit issues
if (builder.Environment.EnvironmentName == "Test" || 
    builder.Environment.EnvironmentName == "Testing" ||
    Environment.GetEnvironmentVariable("DOTNET_DISABLE_FILE_WATCHING") == "true")
{
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
    builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();
    if (args.Length > 0)
    {
        builder.Configuration.AddCommandLine(args);
    }
}

// Validate configuration at startup
builder.Services.AddConfigurationValidation(builder.Configuration, builder.Environment);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Configure SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add JWT service for secure token operations
builder.Services.AddSingleton<IJwtService, JwtService>();

// Configure JWT authentication with secure validation
// Create a temporary JWT service to validate the key at startup
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var startupLogger = loggerFactory.CreateLogger<JwtService>();
var tempJwtService = new JwtService(builder.Configuration, startupLogger);

// Get the validated key for JWT bearer configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? throw new InvalidOperationException("JWT secret key must be configured");
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ClockSkew = TimeSpan.Zero
    };
    
    // Configure JWT authentication for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/progressHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Register application services in logical groups
RegisterCoreServices(builder.Services);
RegisterSecurityServices(builder.Services);
RegisterDatabaseServices(builder.Services);
RegisterProcessingServices(builder.Services);
RegisterTaskServices(builder.Services);

// Configure HttpClient
builder.Services.AddHttpClient();

// Configure CORS with secure, environment-specific settings
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Get allowed origins from environment variable or configuration
        var allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS");
        var allowedOrigins = !string.IsNullOrEmpty(allowedOriginsEnv) 
            ? allowedOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(origin => origin.Trim())
                .Where(origin => !string.IsNullOrEmpty(origin))
                .ToArray()
            : builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        
        if (builder.Environment.IsProduction())
        {
            // Production: Only allow explicitly configured origins
            if (allowedOrigins != null && allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins);
                
                // Log configured origins for security audit
                var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("CORS");
                logger.LogInformation("Production CORS configured with origins: {Origins}", 
                    string.Join(", ", allowedOrigins));
            }
            else
            {
                // In production, CORS must be explicitly configured
                throw new InvalidOperationException(
                    "Production environment requires explicit CORS configuration via ALLOWED_ORIGINS environment variable");
            }
        }
        else
        {
            // Development: Allow configured origins or secure localhost defaults
            if (allowedOrigins != null && allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins);
            }
            else
            {
                // Secure localhost origins for development
                policy.WithOrigins(
                    "http://localhost:3000",    // React dev server
                    "http://localhost:8080",    // Docker container (old port)
                    "http://localhost:8082",    // Docker container (new port)
                    "http://127.0.0.1:3000",
                    "http://127.0.0.1:8080",    // Docker container (old port)
                    "http://127.0.0.1:8082"     // Docker container (new port)
                );
            }
            
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("CORS");
            logger.LogInformation("Development CORS configured with localhost origins");
        }
        
        // Secure header and method configuration
        policy.WithHeaders("Content-Type", "Authorization", "X-Requested-With", "Accept", "Origin")
              .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Exception handling (must be first)
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Security middleware (order is important)
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ProgressHub>("/progressHub");

// Serve React frontend
app.UseDefaultFiles();
app.UseStaticFiles();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.Run();

/// <summary>
/// Register core application services
/// </summary>
static void RegisterCoreServices(IServiceCollection services)
{
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<IImmichService, ImmichService>();
    services.AddScoped<IImageProcessingService, ImageProcessingService>();
}

/// <summary>
/// Register security-related services
/// </summary>
static void RegisterSecurityServices(IServiceCollection services)
{
    services.AddSingleton<ISecureFileService, SecureFileService>();
}

/// <summary>
/// Register centralized database services
/// </summary>
static void RegisterDatabaseServices(IServiceCollection services)
{
    services.AddScoped<IDatabaseService, DatabaseService>();
    services.AddScoped<IConfigurationService, ConfigurationService>();
    services.AddScoped<ITaskRepository, TaskRepository>();
}

/// <summary>
/// Register memory-efficient processing services
/// </summary>
static void RegisterProcessingServices(IServiceCollection services)
{
    services.AddScoped<IStreamingDownloadService, StreamingDownloadService>();
    services.AddScoped<IStreamingResizeService, StreamingResizeService>();
}

/// <summary>
/// Register simplified task execution services
/// </summary>
static void RegisterTaskServices(IServiceCollection services)
{
    services.AddSingleton<TaskExecutor>();
    services.AddHostedService(provider => provider.GetRequiredService<TaskExecutor>());
}

// Make Program class accessible for testing
public partial class Program { }