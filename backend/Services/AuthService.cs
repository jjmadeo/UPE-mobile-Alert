using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Dtos;

namespace MobileAlert.Api.Services;

public class InvalidLoginException(string message) : Exception(message);

/// <summary>Contrato mínimo que tiene que exponer el backend de un cuartel
/// para integrarse acá: recibe usuario/contraseña por POST y devuelve 200
/// con este shape si son válidos, o cualquier otro status si no.</summary>
public record ExternalLoginResponseDto(string? ExternalId, string Name);

public class AuthService(AppDbContext db, JwtTokenService tokens, IHttpClientFactory httpClientFactory)
{
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var institution = await db.Institutions
            .FirstOrDefaultAsync(i => i.Code == request.InstitutionCode, ct)
            ?? throw new InvalidLoginException("Institución no encontrada.");

        var firefighter = string.IsNullOrWhiteSpace(institution.LoginBackendUrl)
            ? await LoginLocalAsync(institution, request, ct)
            : await LoginDelegatedAsync(institution, request, ct);

        var token = tokens.GenerateToken(firefighter);

        return new LoginResponseDto(
            Token: token,
            Firefighter: new FirefighterDto(firefighter.Id.ToString(), firefighter.Name, firefighter.Username),
            Branding: new BrandingDto(
                InstitutionCode: institution.Code,
                InstitutionName: institution.Name,
                PrimaryColor: institution.PrimaryColor,
                LogoUrl: institution.LogoUrl,
                BackendUrl: string.Empty
            )
        );
    }

    /// <summary>Instituciones sin backend propio (las mock sembradas por
    /// DbSeeder) — validamos la contraseña acá mismo, como hacía
    /// mock-server, pero con BCrypt en vez de texto plano.</summary>
    private async Task<Firefighter> LoginLocalAsync(
        Institution institution, LoginRequestDto request, CancellationToken ct)
    {
        var firefighter = await db.Firefighters
            .FirstOrDefaultAsync(f => f.InstitutionId == institution.Id && f.Username == request.Username, ct)
            ?? throw new InvalidLoginException("Usuario o contraseña incorrectos.");

        bool validPassword;
        try
        {
            validPassword = firefighter.PasswordHash is not null
                && BCrypt.Net.BCrypt.Verify(request.Password, firefighter.PasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            validPassword = false;
        }

        if (!validPassword)
        {
            throw new InvalidLoginException("Usuario o contraseña incorrectos.");
        }

        return firefighter;
    }

    /// <summary>Instituciones con backend propio: le reenviamos
    /// usuario/contraseña tal cual los mandó la app (por eso
    /// LoginBackendUrl tiene que ser HTTPS siempre en producción — la
    /// contraseña real del bombero pasa por acá, aunque sea de paso, nunca
    /// se guarda). Si valida, upsert local del Firefighter — necesitamos un
    /// id propio y estable para asociar dispositivos/respuestas, aunque la
    /// identidad "de verdad" viva en el sistema del cuartel.</summary>
    private async Task<Firefighter> LoginDelegatedAsync(
        Institution institution, LoginRequestDto request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("login-delegation");
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                institution.LoginBackendUrl,
                new { username = request.Username, password = request.Password },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidLoginException(
                "No se pudo validar contra el sistema de la institución. Intentá de nuevo en unos minutos.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidLoginException("Usuario o contraseña incorrectos.");
        }

        var externalLogin = await response.Content.ReadFromJsonAsync<ExternalLoginResponseDto>(ct)
            ?? throw new InvalidLoginException("Respuesta inválida del sistema de la institución.");

        var firefighter = await db.Firefighters
            .FirstOrDefaultAsync(f => f.InstitutionId == institution.Id && f.Username == request.Username, ct);

        if (firefighter is null)
        {
            firefighter = new Firefighter
            {
                Name = externalLogin.Name,
                Username = request.Username,
                PasswordHash = null,
                InstitutionId = institution.Id,
            };
            db.Firefighters.Add(firefighter);
        }
        else if (firefighter.Name != externalLogin.Name)
        {
            // El nombre puede haber cambiado del lado del cuartel — se
            // mantiene sincronizado en cada login.
            firefighter.Name = externalLogin.Name;
        }

        await db.SaveChangesAsync(ct);
        return firefighter;
    }
}
