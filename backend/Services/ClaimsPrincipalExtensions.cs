using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MobileAlert.Api.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del bombero autenticado, del claim `sub` del JWT (ver
    /// JwtTokenService.GenerateToken). Solo se debe llamar en endpoints con
    /// `.RequireAuthorization()` — ahí el claim siempre está.</summary>
    public static int GetFirefighterId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token sin claim 'sub' — endpoint mal protegido.");
        return int.Parse(sub);
    }

    /// <summary>Institución resuelta de la API key (ver
    /// ApiKeyAuthenticationHandler) — solo válido en endpoints protegidos
    /// por el scheme ApiKey.</summary>
    public static int GetInstitutionId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ApiKeyAuth.InstitutionIdClaim)
            ?? throw new InvalidOperationException("Token sin claim 'institutionId' — endpoint mal protegido.");
        return int.Parse(value);
    }
}
