using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace SellBotLk.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Authenticates an admin user and returns a JWT token.
    /// Credentials are validated against app configuration.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var adminUser = _config["Admin:Username"];
        var adminPass = _config["Admin:Password"];

        // Treat missing/empty config as defaults for local/demo usage.
        // (appsettings.json may contain empty strings which should not disable login)
        if (string.IsNullOrWhiteSpace(adminUser)) adminUser = "admin";
        if (string.IsNullOrWhiteSpace(adminPass)) adminPass = "admin";

        if (request.Username != adminUser || request.Password != adminPass)
            return Unauthorized(new { message = "Invalid credentials" });

        var token = GenerateJwtToken(request.Username);
        return Ok(new { token });
    }

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
