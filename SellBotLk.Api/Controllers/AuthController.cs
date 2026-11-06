using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace SellBotLk.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    public const int MinPasswordLength = 12;

    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>Program.cs refuses to start unless the configured password passes this.</summary>
    public static bool IsAcceptablePassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= MinPasswordLength;

    /// <summary>
    /// Authenticates an admin user and returns a JWT token.
    /// Credentials are validated against app configuration. There are no defaults:
    /// with no usable credentials configured, nobody can log in.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var adminUser = _config["Admin:Username"];
        var adminPass = _config["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminUser) || !IsAcceptablePassword(adminPass))
        {
            _logger.LogError("Admin login is disabled: credentials are not configured properly");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Admin login is not configured" });
        }

        // "&" rather than "&&" so a wrong username takes as long as a wrong password.
        var isValid = SecretEquals(request.Username, adminUser) &
                      SecretEquals(request.Password, adminPass!);

        if (!isValid)
        {
            _logger.LogWarning("Failed admin login from {Ip}", HttpContext?.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = GenerateJwtToken(request.Username);
        return Ok(new { token });
    }

    // Hashing first gives both sides the same length for a constant-time comparison.
    private static bool SecretEquals(string supplied, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    private string GenerateJwtToken(string username)
    {
        var secret = _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var token = new JwtSecurityToken(
            issuer: "SellBotLk",
            audience: "SellBotLk.Admin",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}
