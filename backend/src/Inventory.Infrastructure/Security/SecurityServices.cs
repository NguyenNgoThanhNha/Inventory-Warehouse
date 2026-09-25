using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities.Sys;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "inventory-api";
    public string Audience { get; set; } = "inventory-web";

    /// <summary>Tối thiểu 32 ký tự. Production đặt qua user-secrets / biến môi trường.</summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

/// <summary>Access token chỉ chứa định danh (sub, email, name) — quyền tra qua IPermissionService.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(SysAccount user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, now, expires, credentials);
        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string GenerateSecureToken() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
}

/// <summary>PBKDF2 qua PasswordHasher của ASP.NET Core Identity.</summary>
public sealed class PasswordHasherAdapter : Application.Common.Interfaces.IPasswordHasher
{
    private readonly PasswordHasher<SysAccount> _hasher = new();

    public string Hash(SysAccount user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(SysAccount user, string password) =>
        !string.IsNullOrEmpty(user.PasswordHash)
        && _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
