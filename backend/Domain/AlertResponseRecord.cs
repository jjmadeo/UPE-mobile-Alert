namespace MobileAlert.Api.Domain;

public enum AlertResponseType
{
    Attending,
    NotAttending,
}

/// <summary>
/// La respuesta de un bombero a un aviso — "Asistir" o "No asistir", con la
/// ubicación que tenía en ese momento (si la pudo obtener a tiempo, ver
/// getCurrentLocation.ts del lado de la app).
/// </summary>
public class AlertResponseRecord : IAuditable
{
    public int Id { get; set; }

    public Guid AlertId { get; set; }
    public AlertRecord Alert { get; set; } = null!;

    public int FirefighterId { get; set; }
    public Firefighter Firefighter { get; set; } = null!;

    public AlertResponseType Response { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Accuracy { get; set; }

    /// <summary>Timestamp que manda el CLIENTE (la app) — de negocio, no
    /// de auditoría: "cuándo el bombero dice que respondió". Puede diferir
    /// un poco de `CreatedAt`/`UpdatedAt` (cuándo la fila se creó/tocó acá)
    /// por latencia de red.</summary>
    public DateTime RespondedAt { get; set; } = DateTime.UtcNow;

    // --- Auditoría de la entrega del webhook (ver WebhookNotifier) ---
    // Nulos si la institución no tenía ningún webhook activo en ese
    // momento. Asume UN webhook por institución: si algún día se admite
    // más de uno activo a la vez, esto solo guarda el resultado del
    // ÚLTIMO intentado — para eso hace falta una tabla de delivery log
    // aparte, no vale la pena hoy con un solo webhook por cuartel en la
    // práctica.
    /// <summary>A qué URL se mandó (snapshot — si el cuartel cambia la URL
    /// de su webhook después, esto sigue mostrando la que era en ese
    /// momento).</summary>
    public string? WebhookUrl { get; set; }
    /// <summary>El body que le mandamos (firmado, mismo JSON que viaja en
    /// el POST con header X-Signature).</summary>
    public string? WebhookRequestPayload { get; set; }
    /// <summary>Lo que devolvió el servidor del cuartel — texto plano, no
    /// jsonb: no controlamos qué nos manda de vuelta (podría no ser JSON
    /// siquiera, ej. una página de error).</summary>
    public string? WebhookResponsePayload { get; set; }
    public int? WebhookStatusCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
