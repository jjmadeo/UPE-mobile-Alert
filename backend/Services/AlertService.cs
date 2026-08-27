using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Dtos;

namespace MobileAlert.Api.Services;

/// <summary>Resultado de crear una alerta. Siempre se prioriza mandar lo
/// que se pueda: `Alert` nunca es null, aunque CERO de los
/// `FirefighterIds` pedidos hayan sido válidos (ver AlertsEndpoints — esto
/// siempre responde 200, el desglose de qué falló va en el body, no en el
/// status code).</summary>
public record AlertCreationResult(
    AlertRecord Alert,
    int DevicesNotified,
    int[] UnknownFirefighterIds,
    int[] FirefightersWithoutDevice
);

public class AlertService(AppDbContext db, IFcmSender fcm, WebhookNotifier webhooks, ILogger<AlertService> logger)
{
    /// <summary>Crea la alerta (si hay al menos un target válido) y manda
    /// el primer envío. Los reenvíos mientras siga sin respuesta los
    /// maneja AlertRetryBackgroundService — acá solo se registra el
    /// primero. `institutionId` viene de la API key que hizo el request
    /// (ver AlertsEndpoints), no de un campo en el body.</summary>
    public async Task<AlertCreationResult> CreateAsync(
        int institutionId, CreateAlertRequestDto request, CancellationToken ct = default)
    {
        if (request.CorrelationId == Guid.Empty)
        {
            throw new ArgumentException("correlationId es obligatorio.");
        }
        if (request.FirefighterIds is null || request.FirefighterIds.Length == 0)
        {
            throw new ArgumentException("firefighterIds es obligatorio y no puede estar vacío.");
        }

        // Replay idempotente: reenviar el mismo CorrelationId no crea una
        // alerta duplicada. No se recalcula el desglose de
        // unknown/withoutDevice acá, ni se toca RequestPayload/
        // ResponsePayload — esos quedan como constancia de la creación
        // ORIGINAL, no de cada replay.
        var existingAlert = await db.Alerts
            .FirstOrDefaultAsync(a => a.CorrelationId == request.CorrelationId, ct);
        if (existingAlert is not null)
        {
            logger.LogInformation(
                "CorrelationId {CorrelationId} ya existía (alerta {AlertId}) — replay idempotente, no se reenvía.",
                request.CorrelationId, existingAlert.Id);
            return new AlertCreationResult(existingAlert, 0, [], []);
        }

        var requestedIds = request.FirefighterIds.Distinct().ToArray();

        var firefighters = await db.Firefighters
            .Include(f => f.Devices)
            .Where(f => requestedIds.Contains(f.Id) && f.InstitutionId == institutionId)
            .ToListAsync(ct);

        var foundIds = firefighters.Select(f => f.Id).ToHashSet();
        var unknownIds = requestedIds.Where(id => !foundIds.Contains(id)).ToArray();
        var withoutDevice = firefighters.Where(f => f.Devices.Count == 0).Select(f => f.Id).ToArray();
        var validTargetIds = firefighters.Where(f => f.Devices.Count > 0).Select(f => f.Id).ToArray();

        if (validTargetIds.Length == 0)
        {
            // Ninguno era válido — igual se prioriza dejar constancia y no
            // cortar el flujo con un error: se crea la alerta (target
            // vacío, 0 enviados) y se informa el detalle completo. No es
            // un error del request — el request está bien armado, lo que
            // falló es la resolución de destinatarios, y eso ya queda
            // reportado en el body (ver AlertsEndpoints).
            logger.LogWarning(
                "Alerta para institución {InstitutionId}: ningún firefighterId válido de {Requested} (unknown: {Unknown}, sin device: {WithoutDevice}).",
                institutionId, requestedIds.Length, unknownIds.Length, withoutDevice.Length);
        }

        var alert = new AlertRecord
        {
            CorrelationId = request.CorrelationId,
            Title = request.Title,
            Message = request.Message,
            Address = request.Address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            InstitutionId = institutionId,
            TargetFirefighterIds = validTargetIds,
            RequestPayload = JsonSerializer.Serialize(request),
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);

        var notified = await FanOutAsync(alert, ct);
        alert.LastSentAt = DateTime.UtcNow;

        var result = new AlertCreationResult(alert, notified, unknownIds, withoutDevice);
        // El ResponsePayload incluye el AlertId recién asignado — por eso
        // se serializa acá, después del primer SaveChanges (que ya generó
        // el Id), y no antes.
        alert.ResponsePayload = JsonSerializer.Serialize(new AlertCreatedResponseDto(
            alert.Id, alert.CorrelationId, notified, unknownIds, withoutDevice));
        await db.SaveChangesAsync(ct);

        return result;
    }

    /// <summary>Manda (o re-manda) el push a los dispositivos de los
    /// bomberos en `alert.TargetFirefighterIds` — no a "todos los de la
    /// institución en este momento". Público porque
    /// AlertRetryBackgroundService también lo llama en cada reintento.</summary>
    public async Task<int> FanOutAsync(AlertRecord alert, CancellationToken ct = default)
    {
        var tokens = await db.DeviceTokens
            .Where(d => alert.TargetFirefighterIds.Contains(d.FirefighterId))
            .Select(d => d.FcmToken)
            .ToListAsync(ct);

        var data = new Dictionary<string, string>
        {
            ["alertId"] = alert.Id.ToString(),
            ["correlationId"] = alert.CorrelationId.ToString(),
            ["title"] = alert.Title,
            ["message"] = alert.Message,
            ["address"] = alert.Address ?? "",
            ["latitude"] = alert.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "",
            ["longitude"] = alert.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "",
            // formato "o" (round-trip ISO 8601) — mismo formato que
            // `new Date().toISOString()` del lado de la app.
            ["createdAt"] = alert.CreatedAt.ToString("o"),
        };

        var sent = 0;
        foreach (var token in tokens)
        {
            if (await fcm.SendAsync(token, data, ct))
            {
                sent++;
            }
        }

        logger.LogInformation(
            "Alerta {AlertId} ({Title}): {Sent}/{Total} pushes enviados",
            alert.Id, alert.Title, sent, tokens.Count);

        return sent;
    }

    /// <summary>Ids de bombero targeteados que TODAVÍA no respondieron —
    /// `TargetFirefighterIds` menos los `FirefighterId` que ya aparecen en
    /// AlertResponseRecord para esta alerta. No se usa en el flujo actual
    /// (hoy alcanza con UNA respuesta para dejar de insistirle a todo el
    /// mundo, ver AlertStatus.Answered), pero queda como utilidad — si
    /// algún día se quiere "insistirle solo a quien no contestó todavía"
    /// en vez de pararlo todo con la primera respuesta, es esta resta.</summary>
    public async Task<int[]> GetUnansweredFirefighterIdsAsync(Guid alertId, CancellationToken ct = default)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == alertId, ct)
            ?? throw new KeyNotFoundException("Alerta no encontrada.");

        var responded = await db.AlertResponses
            .Where(r => r.AlertId == alertId)
            .Select(r => r.FirefighterId)
            .ToListAsync(ct);

        return alert.TargetFirefighterIds.Except(responded).ToArray();
    }

    public async Task RespondAsync(int firefighterId, RespondAlertRequestDto request, CancellationToken ct = default)
    {
        if (!Guid.TryParse(request.AlertId, out var alertId))
        {
            throw new ArgumentException("alertId inválido.");
        }

        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == alertId, ct)
            ?? throw new KeyNotFoundException("Alerta no encontrada.");

        var responseType = request.Response switch
        {
            "ATTENDING" => AlertResponseType.Attending,
            "NOT_ATTENDING" => AlertResponseType.NotAttending,
            _ => throw new ArgumentException($"response inválido: {request.Response}"),
        };

        // Upsert: si el bombero ya había respondido antes (se arrepintió,
        // tocó dos veces), se actualiza la misma fila en vez de duplicarla
        // — ver el índice único (AlertId, FirefighterId) en AppDbContext.
        var existing = await db.AlertResponses
            .FirstOrDefaultAsync(r => r.AlertId == alertId && r.FirefighterId == firefighterId, ct);

        if (existing is null)
        {
            existing = new AlertResponseRecord { AlertId = alertId, FirefighterId = firefighterId };
            db.AlertResponses.Add(existing);
        }

        existing.Response = responseType;
        existing.Latitude = request.Location?.Latitude;
        existing.Longitude = request.Location?.Longitude;
        existing.Accuracy = request.Location?.Accuracy;
        existing.RespondedAt = DateTimeOffset.TryParse(request.RespondedAt, out var parsed)
            ? parsed.UtcDateTime
            : DateTime.UtcNow;

        // Con UNA respuesta alcanza para dejar de insistirle a todo el
        // cuartel — ver el comentario en AlertStatus.Answered.
        alert.Status = AlertStatus.Answered;

        await db.SaveChangesAsync(ct);

        // Después de guardar, no antes: si el webhook tarda o falla no
        // tiene que afectar en nada a que la respuesta del bombero haya
        // quedado registrada. WebhookNotifier persiste su propia
        // auditoría (url/request/response — ver AlertResponseRecord) al
        // final, con su propio SaveChanges.
        await webhooks.NotifyResponseAsync(alert, existing, ct);
    }
}
