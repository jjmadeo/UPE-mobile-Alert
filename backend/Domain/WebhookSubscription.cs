namespace MobileAlert.Api.Domain;

/// <summary>
/// URL propia de un cuartel donde le avisamos cada vez que un bombero
/// responde una alerta (asiste/no asiste) — para que su backend no tenga
/// que estar consultándonos (ver el endpoint de debug
/// GET /api/alerts/{id}/responses, que hoy hace polling; el webhook es el
/// reemplazo real de eso).
/// </summary>
public class WebhookSubscription : IAuditable
{
    public int Id { get; set; }

    public int InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string Url { get; set; }

    /// <summary>Secreto compartido para firmar el payload del webhook
    /// (HMAC-SHA256, header X-Signature) — así el cuartel puede verificar
    /// que el request vino realmente de nosotros y no de un tercero que le
    /// pegó a su endpoint público. Se muestra una sola vez, al crear la
    /// suscripción (ver WebhooksEndpoints).</summary>
    public required string Secret { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
