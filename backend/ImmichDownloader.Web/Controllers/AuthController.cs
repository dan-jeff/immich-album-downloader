using Microsoft.AspNetCore.Mvc;
using ImmichDownloader.Web.Services;
using ImmichDownloader.Web.Models;
using ImmichDownloader.Web.Models.Requests;
using ImmichDownloader.Web.Models.Responses;

namespace ImmichDownloader.Web.Controllers;

/// <summary>
/// Controller responsible for authentication operations including user registration,
/// login, and initial setup verification.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="authService">The authentication service for handling user operations.</param>
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Checks if the application requires initial setup by verifying if any users exist.
    /// </summary>
    /// <returns>A JSON response indicating whether setup is required.</returns>
    /// <response code="200">Returns setup status information.</response>
    [HttpGet("check-setup")]
    public async Task<IActionResult> CheckSetup()
    {
        var setupRequired = !await _authService.UserExistsAsync();
        return Ok(new { setup_required = setupRequired });
    }

    /// <summary>
    /// Registers a new user account. Only allows registration if no users exist yet.
    /// </summary>
    /// <param name="request">The registration request containing username and password.</param>
    /// <returns>A result indicating success or failure of the registration.</returns>
    /// <response code="200">User created successfully.</response>
    /// <response code="400">Invalid request data or user already exists.</response>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (await _authService.UserExistsAsync())
            return BadRequest(new { detail = "User already exists" });

        var success = await _authService.CreateUserAsync(request.Username, request.Password);
        if (!success)
            return BadRequest(new { detail = "Failed to create user" });

        return Ok(new { message = "User created successfully" });
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token for subsequent API requests.
    /// </summary>
    /// <param name="request">The login request containing username and password.</param>
    /// <returns>A JWT token if authentication is successful, otherwise an error.</returns>
    /// <response code="200">Authentication successful, returns JWT token.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var token = await _authService.AuthenticateAsync(request.Username, request.Password);
        if (token == null)
            return Unauthorized(new { detail = "Incorrect username or password" });

        return Ok(new TokenResponse { access_token = token });
    }
}