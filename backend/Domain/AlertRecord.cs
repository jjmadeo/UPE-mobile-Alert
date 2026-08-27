namespace MobileAlert.Api.Domain;

public enum AlertStatus
{
    /// <summary>Todavía nadie respondió — el AlertRetryBackgroundService le
    /// sigue insistiendo a la institución.</summary>
    Pending,

    /// <summary>Al menos un bombero respondió — se dejó de insistir. (Se
    /// eligió "al menos uno" y no "todos": una vez que alguien confirma que
    /// va, no tiene sentido seguir despertando a todo el cuartel — ver
    /// AlertRetryBackgroundService.)</summary>
    Answered,

    /// <summary>Se llegó al máximo de reintentos sin ninguna respuesta.</summary>
    Expired,
}

/// <summary>
/// Un aviso de emergencia mandado a una institución. Es el equivalente
/// "real" de lo que hoy arma mock-server/send-test-alert.js a mano en cada
/// llamada — acá queda persistido y su fan-out lo maneja
/// AlertRetryBackgroundService en vez de un script.
/// </summary>
public class AlertRecord : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Generado por el backend del cuartel, no por nosotros — es
    /// el identificador "de ellos" para este aviso, no nuestra PK. Viaja
    /// hasta el dispositivo y vuelve en cada webhook. Único: reenviar el
    /// mismo CorrelationId es un replay idempotente, no un aviso nuevo —
    /// ver AlertService.CreateAsync.</summary>
    public Guid CorrelationId { get; set; }

    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? Address { get; set; }

    /// <summary>Coordenadas del siniestro, opcionales — si están, la app le
    /// permite al bombero calcular la distancia contra su propia ubicación
    /// (ver AlertScreen.tsx / src/location/distance.ts).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Ids de bombero VÁLIDOS a los que se les mandó esta alerta
    /// (existían en la institución y tenían ≥1 DeviceToken) — el
    /// subconjunto de `RequestPayload.firefighterIds` que realmente recibió
    /// el push. Reemplaza a lo que antes era una tabla AlertTarget aparte:
    /// mismo dato, sin una fila por bombero por alerta. Array nativo de
    /// Postgres (int[]), no JSON — se puede seguir consultando con `@>` si
    /// hiciera falta, y AlertRetryBackgroundService lo lee en cada tick sin
    /// tener que parsear RequestPayload entero. "¿Quién no respondió
    /// todavía?" = este array menos los FirefighterId que aparecen en
    /// AlertResponseRecord para este AlertId.</summary>
    public int[] TargetFirefighterIds { get; set; } = [];

    /// <summary>Body crudo tal cual llegó en POST /api/alerts (el
    /// CreateAlertRequestDto serializado) — auditoría/debug: poder ver
    /// exactamente qué mandó el cuartel, más allá de lo que capturan las
    /// columnas tipadas de arriba.</summary>
    public string? RequestPayload { get; set; }

    /// <summary>Body de la respuesta que le devolvimos al cuartel
    /// (AlertCreatedResponseDto serializado) en el momento de la creación
    /// original — no se recalcula en un replay idempotente, es la
    /// constancia de qué pasó la primera vez.</summary>
    public string? ResponsePayload { get; set; }

    public AlertStatus Status { get; set; } = AlertStatus.Pending;

    /// <summary>Cuántas veces se reenvió hasta ahora (0 = todavía solo el
    /// envío inicial).</summary>
    public int RetryCount { get; set; }
    /// <summary>Distinto de `UpdatedAt`: esto es de negocio — la última vez
    /// que se intentó un fan-out (inicial o reintento), no "la última vez
    /// que cambió algún campo de la fila".</summary>
    public DateTime? LastSentAt { get; set; }

    public int InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public ICollection<AlertResponseRecord> Responses { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
