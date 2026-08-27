namespace MobileAlert.Api.Domain;

/// <summary>
/// Un bombero con cuenta en la app. La contraseña nunca se guarda en texto
/// plano (a diferencia del mock-server) — ver AuthService, que usa BCrypt.
/// </summary>
public class Firefighter : IAuditable
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Único DENTRO de la institución, no globalmente — dos
    /// instituciones distintas pueden tener cada una un "juan".</summary>
    public required string Username { get; set; }

    /// <summary>Null cuando la institución tiene LoginBackendUrl propio —
    /// ahí la contraseña la valida el backend del cuartel, no nosotros (ver
    /// AuthService.LoginAsync), así que no hay nada que guardar acá.</summary>
    public string? PasswordHash { get; set; }

    public int InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public ICollection<DeviceToken> Devices { get; set; } = [];
    public ICollection<AlertResponseRecord> Responses { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
