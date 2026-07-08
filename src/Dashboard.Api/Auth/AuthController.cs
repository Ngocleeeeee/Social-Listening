using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Dashboard.Api.Auth;

public sealed record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IConfiguration cfg) : ControllerBase
{
    /// <summary>POST /api/auth/login — demo credentials from config → JWT (8h).</summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        var user = cfg["Auth:Username"] ?? "admin";
        var pass = cfg["Auth:Password"] ?? "admin123";
        if (req.Username != user || req.Password != pass)
            return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"], audience: cfg["Jwt:Audience"],
            claims: new[] { new Claim(ClaimTypes.Name, req.Username) },
            expires: DateTime.UtcNow.AddHours(8), signingCredentials: creds);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), user = req.Username });
    }
}
