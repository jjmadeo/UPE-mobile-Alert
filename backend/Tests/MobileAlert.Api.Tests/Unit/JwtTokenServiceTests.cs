using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Services;
using Xunit;

namespace MobileAlert.Api.Tests.Unit;

/// <summary>El JWT que se manda de acá es la credencial de TODA la sesión
/// de la app (ver src/api/client.ts del lado mobile) — si el claim del id
/// del bombero no calzara con lo que espera
/// ClaimsPrincipalExtensions.GetFirefighterId, o la firma no validara con el
/// secreto configurado en Program.cs, cualquier request autenticado de la
/// app rompería.</summary>
public class JwtTokenServiceTests
{
    private const string Secret = "unit-test-secret-de-al-menos-32-caracteres";
    private const string Issuer = "mobile-alert-api";

    private static JwtTokenService BuildService(string secret = Secret, int expirationHours = 1) =>
        new(Options.Create(new JwtOptions { Secret = secret, Issuer = Issuer, ExpirationHours = expirationHours }));

    [Fact]
    public void GenerateToken_EncodesFirefighterIdAndInstitutionId_AsRawClaims()
    {
        var service = BuildService();
        var firefighter = new Firefighter { Id = 42, InstitutionId = 7, Name = "Juan Pérez", Username = "juan" };

        var token = service.GenerateToken(firefighter);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // "sub" tal cual (sin remapear) — es justo lo que
        // ClaimsPrincipalExtensions.GetFirefighterId busca. Y "institutionId"
        // en un JWT de bombero es solo para debug/logging: los endpoints que
        // necesitan institución de verdad la sacan de la API key del
        // cuartel (GetInstitutionId), no de acá.
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("7", jwt.Claims.Single(c => c.Type == "institutionId").Value);
    }

    [Fact]
    public void GenerateToken_TwoCallsForTheSameFirefighter_ProduceDifferentTokens()
    {
        var service = BuildService();
        var firefighter = new Firefighter { Id = 1, InstitutionId = 1, Name = "Juan", Username = "juan" };

        // El claim "jti" es un Guid random en cada llamada — dos logins
        // seguidos del mismo bombero no pueden generar el mismo token (si
        // no, no tendría sentido que cada login devuelva uno "nuevo").
        Assert.NotEqual(service.GenerateToken(firefighter), service.GenerateToken(firefighter));
    }

    [Fact]
    public void GenerateToken_ValidatesCorrectly_WithTheSameParametersProgramCsUses()
    {
        var service = BuildService();
        var firefighter = new Firefighter { Id = 1, InstitutionId = 1, Name = "Juan", Username = "juan" };
        var token = service.GenerateToken(firefighter);

        // Misma validación que configura Program.cs para proteger los
        // endpoints con JWT (issuer/audience/firma/expiración, y
        // MapInboundClaims = false) — si esto no valida y deja el claim
        // "sub" tal cual, ningún login real funcionaría tampoco. Sin
        // MapInboundClaims = false acá, ValidateToken remapea "sub" a una
        // claim URI larga (herencia WS-Federation) y este mismo Assert
        // falla con NullReferenceException — es el bug real que ya se
        // encontró una vez en Program.cs, ver ese comentario.
        var jwtHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = jwtHandler.ValidateToken(token, ValidationParametersFor(Secret), out _);

        Assert.Equal("1", principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
    }

    [Fact]
    public void GenerateToken_WithADifferentSecretThanTheOneConfigured_FailsValidation()
    {
        var service = BuildService();
        var firefighter = new Firefighter { Id = 1, InstitutionId = 1, Name = "Juan", Username = "juan" };
        var token = service.GenerateToken(firefighter);

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(
                token, ValidationParametersFor("un-secreto-distinto-al-configurado-32c"), out _));
    }

    [Fact]
    public void GenerateToken_AlreadyExpired_FailsValidation()
    {
        // ExpirationHours negativo → "expiró" ya en el momento de emitirse.
        var service = BuildService(expirationHours: -1);
        var firefighter = new Firefighter { Id = 1, InstitutionId = 1, Name = "Juan", Username = "juan" };
        var token = service.GenerateToken(firefighter);

        Assert.Throws<SecurityTokenExpiredException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(token, ValidationParametersFor(Secret), out _));
    }

    private static TokenValidationParameters ValidationParametersFor(string secret) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Issuer,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
    };
}
