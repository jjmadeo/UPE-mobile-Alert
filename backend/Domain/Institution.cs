namespace MobileAlert.Api.Domain;

/// <summary>
/// Un cuartel/institución. Es la unidad de white-label: cada una tiene su
/// propio color y (a futuro) su propio backendUrl — ver BrandingConfig del
/// lado de la app (src/types/branding.ts).
/// </summary>
public class Institution : IAuditable
{
    public int Id { get; set; }

    /// <summary>Código único, ej. "BOMBEROS-CENTRAL". Es lo que el bombero
    /// tipea en el login.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>Hex, ej. "#1E3A8A". Se usa como acento en la app, nunca como
    /// fondo completo — ver HomeScreen.tsx.</summary>
    public required string PrimaryColor { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>URL del backend PROPIO del cuartel que valida usuario y
    /// contraseña — ver AuthService.LoginAsync. Si es null, se usa el
    /// fallback local (Firefighter.PasswordHash acá mismo), que es lo que
    /// usan las instituciones mock sembradas por DbSeeder.</summary>
    public string? LoginBackendUrl { get; set; }

    public ICollection<Firefighter> Firefighters { get; set; } = [];
    public ICollection<AlertRecord> Alerts { get; set; } = [];
    public ICollection<ApiKeyRecord> ApiKeys { get; set; } = [];
    public ICollection<WebhookSubscription> Webhooks { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
