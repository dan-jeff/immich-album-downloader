using ImmichDownloader.Web.Data;
using ImmichDownloader.Web.Services;
using ImmichDownloader.Web.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Configure SQLite database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ?? throw new InvalidOperationException("JWT secret key must be configured via Jwt:Key in appsettings.json or JWT_SECRET_KEY environment variable");
var key = Encoding.ASCII.GetBytes(jwtKey);

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

// Add application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IImmichService, ImmichService>();
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();
builder.Services.AddSingleton<ITaskProgressService, TaskProgressService>();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
// Legacy services (kept for compatibility)
builder.Services.AddScoped<IDownloadService, DownloadService>();
builder.Services.AddScoped<IResizeService, ResizeService>();
// New streaming services for large downloads
builder.Services.AddScoped<IStreamingDownloadService, StreamingDownloadService>();
builder.Services.AddScoped<IStreamingResizeService, StreamingResizeService>();
builder.Services.AddHostedService<BackgroundTaskService>();

// Configure HttpClient
builder.Services.AddHttpClient();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        
        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            // Use specific origins if configured
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            // Default origins for Docker deployment (localhost and common local IPs)
            policy.WithOrigins(
                "http://localhost:8080",
                "http://127.0.0.1:8080",
                "http://192.168.68.21:8080",
                "http://172.17.0.1:8080",
                "http://172.18.0.1:8080",
                "http://10.0.0.1:8080"
            ).SetIsOriginAllowedToAllowWildcardSubdomains();
        }
        
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Always allow credentials for JWT tokens
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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