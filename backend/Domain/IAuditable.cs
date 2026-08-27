namespace MobileAlert.Api.Domain;

/// <summary>
/// Timestamps de auditoría estandarizados en TODAS las entidades del
/// modelo. Los completa solo `AppDbContext` (ver el override de
/// SaveChanges/SaveChangesAsync) — nunca a mano dentro de un servicio, para
/// que no dependa de que cada método se acuerde de actualizarlos. Esto es
/// aparte de cualquier timestamp con significado de negocio propio que ya
/// tenga la entidad (ej. `AlertRecord.LastSentAt`, `AlertResponseRecord.
/// RespondedAt`) — esos siguen existiendo y significan otra cosa.
/// </summary>
public interface IAuditable
{
    /// <summary>Cuándo se insertó la fila. Lo pone SaveChanges en el
    /// momento del INSERT — no antes (evita que difiera del timestamp real
    /// si el objeto se construyó bastante antes de guardarse).</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>Null hasta el primer UPDATE. Cambia en cada SaveChanges
    /// donde la entidad tenga algún campo modificado — sirve para
    /// responder "¿esta fila cambió después de creada, y cuándo fue la
    /// última vez?" sin tener que llevar un log de cambios aparte.</summary>
    DateTime? UpdatedAt { get; set; }
}
