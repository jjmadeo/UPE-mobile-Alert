using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MobileAlert.Api.Data;

namespace MobileAlert.Api.Services;

public static class ApiKeyAuth
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    /// <summary>Claim con el InstitutionId resuelto de la key — lo leen los
    /// endpoints protegidos por este scheme (ver AlertsEndpoints,
    /// WebhooksEndpoints) en vez de confiar en un institutionCode que venga
    /// en el body.</summary>
    public const string InstitutionIdClaim = "institutionId";

    public static string Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Valida el header `X-Api-Key` contra ApiKeyRecord.KeyHash. Es el
/// mecanismo de auth para los endpoints que llama el BACKEND de un cuartel
/// (no la app mobile, que sigue usando JWT) — mandar alertas, registrar
/// webhooks.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuth.HeaderName, out var provided) || provided.Count == 0)
        {
            return AuthenticateResult.Fail($"Falta el header {ApiKeyAuth.HeaderName}.");
        }

        var hash = ApiKeyAuth.Hash(provided.ToString());
        var apiKey = await db.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);

        if (apiKey is null)
        {
            return AuthenticateResult.Fail("API key inválida.");
        }

        var claims = new[]
        {
            new Claim(ApiKeyAuth.InstitutionIdClaim, apiKey.InstitutionId.ToString()),
            new Claim(ClaimTypes.Name, apiKey.Name),
        };
        var identity = new ClaimsIdentity(claims, ApiKeyAuth.SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuth.SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
