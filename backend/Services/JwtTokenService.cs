using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MobileAlert.Api.Domain;

namespace MobileAlert.Api.Services;

public class JwtOptions
{
    public required string Secret { get; set; }
    public required string Issuer { get; set; }
    public int ExpirationHours { get; set; } = 24 * 30;
}

/// <summary>
/// Reemplaza el `mock-token-${Date.now()}-...` sin firmar del mock-server
/// por un JWT de verdad (HS256), firmado con un secreto que solo conoce
/// este backend.
/// </summary>
public class JwtTokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string GenerateToken(Firefighter firefighter)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, firefighter.Id.ToString()),
            new Claim("institutionId", firefighter.InstitutionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_options.ExpirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
