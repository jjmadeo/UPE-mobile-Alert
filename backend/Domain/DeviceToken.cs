namespace MobileAlert.Api.Domain;

/// <summary>
/// Token FCM de un dispositivo. Un bombero puede tener más de uno (cambió
/// de teléfono y el viejo token no se borró todavía) — el fan-out de una
/// alerta le manda el push a todos los tokens vigentes de cada bombero de
/// la institución.
/// </summary>
public class DeviceToken : IAuditable
{
    public int Id { get; set; }

    public required string FcmToken { get; set; }

    /// <summary>Distinto de `CreatedAt`/`UpdatedAt` (auditoría genérica):
    /// esto es de negocio — "la última vez que confirmamos que este token
    /// sigue siendo válido para este bombero" (se toca en cada login, ver
    /// DeviceService.RegisterAsync), no "cuándo se modificó la fila".</summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public int FirefighterId { get; set; }
    public Firefighter Firefighter { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
