namespace MobileAlert.Api.Domain;

/// <summary>
/// Credencial de un backend externo (el sistema propio de un cuartel) para
/// llamar a nuestra API en su nombre — mandar alertas, registrar webhooks.
/// Se guarda hasheada (SHA-256, no BCrypt: una API key es un secreto de
/// alta entropía generado por nosotros, no una contraseña humana, así que
/// no hace falta un hash lento con salt) — nunca el valor en texto plano.
/// El valor real solo existe una vez, en el momento en que se genera; si se
/// pierde, hay que rotarla.
/// </summary>
public class ApiKeyRecord : IAuditable
{
    public int Id { get; set; }

    /// <summary>SHA-256 en hex del valor real de la key. Ver el comentario
    /// de ejemplo en README del backend para el snippet de cómo generar
    /// una a mano mientras no hay endpoint de administración.</summary>
    public required string KeyHash { get; set; }

    /// <summary>Etiqueta humana, ej. "Sistema de despacho — Bomberos
    /// Central", solo para identificarla en la base.</summary>
    public required string Name { get; set; }

    public int InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
