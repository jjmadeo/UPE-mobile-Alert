using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;
using MobileAlert.Api.Dtos;
using MobileAlert.Api.Services;

namespace MobileAlert.Api.Endpoints;

public static class AlertsEndpoints
{
    public static void MapAlertsEndpoints(this IEndpointRouteBuilder app)
    {
        // La llama el BACKEND del cuartel (con su API key), no un bombero —
        // ver ApiKeyAuthenticationHandler. La institución la determina la
        // key, no un campo del body: una key de un cuartel no puede mandar
        // alertas a nombre de otro.
        app.MapPost("/api/alerts", async (
            CreateAlertRequestDto request,
            ClaimsPrincipal user,
            AlertService alerts,
            CancellationToken ct) =>
        {
            try
            {
                // Siempre 200: se prioriza mandar lo que se pueda mandar.
                // Si algún firefighterId falló (no existe / sin device),
                // el detalle va en el body — nunca en el status code, así
                // el llamador no tiene que distinguir "falló todo" de
                // "falló parte" a nivel HTTP, solo mirar los arrays.
                var result = await alerts.CreateAsync(user.GetInstitutionId(), request, ct);
                return Results.Ok(new AlertCreatedResponseDto(
                    result.Alert.Id,
                    result.Alert.CorrelationId,
                    result.DevicesNotified,
                    result.UnknownFirefighterIds,
                    result.FirefightersWithoutDevice
                ));
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .RequireAuthorization(ApiKeyAuth.SchemeName)
        .WithName("CreateAlert")
        .WithSummary("Dispara una alerta a bomberos puntuales de tu institución")
        .WithDescription(
            "Lo llama el BACKEND DEL CUARTEL, autenticado con su API key (header X-Api-Key). " +
            "Siempre devuelve 200: se prioriza mandar el push a los firefighterIds que sí son " +
            "válidos, aunque otros no lo sean — el detalle de qué id falló va en el body " +
            "(unknownFirefighterIds / firefightersWithoutDevice), nunca en el status code. " +
            "correlationId lo generás vos (UUID) — reenviar el mismo es un replay idempotente, " +
            "no crea una alerta nueva, y es el mismo valor que después vuelve en el webhook de " +
            "cada respuesta. Reintenta el push cada 10s (configurable) hasta que alguien " +
            "responda o se agoten los reintentos.")
        .WithTags("Backend del cuartel");

        app.MapPost("/api/alerts/{alertId}/response", async (
            string alertId,
            RespondAlertRequestDto body,
            ClaimsPrincipal user,
            AlertService alerts,
            CancellationToken ct) =>
        {
            // El alertId de la ruta manda — se ignora el del body si no
            // coincide, para que no se pueda responder una alerta distinta
            // a la que dice la URL.
            var request = body with { AlertId = alertId };
            try
            {
                await alerts.RespondAsync(user.GetFirefighterId(), request, ct);
                return Results.Ok(new { ok = true });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .RequireAuthorization()
        .WithName("RespondToAlert")
        .WithSummary("El bombero responde ATTENDING / NOT_ATTENDING")
        .WithDescription(
            "Lo llama la APP MOBILE, con el JWT del login. Una vez que UN bombero responde, se " +
            "deja de reintentar el push a toda la institución. Dispara el webhook del cuartel " +
            "(si tiene uno registrado) con la respuesta.")
        .WithTags("Bombero (app mobile)");

        // Debug, sin auth — igual que en mock-server/server.js, para poder
        // chequear respuestas fácil durante pruebas. Con los webhooks
        // (ver WebhooksEndpoints) ya no debería hacer falta que nadie
        // haga polling acá — queda solo para debug manual.
        app.MapGet("/api/alerts/{alertId}/responses", async (
            string alertId, AppDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(alertId, out var id))
            {
                return Results.BadRequest(new { message = "alertId inválido." });
            }

            var responses = await db.AlertResponses
                .Where(r => r.AlertId == id)
                .Select(r => new
                {
                    alertId = r.AlertId,
                    correlationId = r.Alert.CorrelationId,
                    firefighterId = r.FirefighterId,
                    response = r.Response == Domain.AlertResponseType.Attending ? "ATTENDING" : "NOT_ATTENDING",
                    location = r.Latitude != null
                        ? new { latitude = r.Latitude, longitude = r.Longitude, accuracy = r.Accuracy }
                        : null,
                    respondedAt = r.RespondedAt,
                })
                .ToListAsync(ct);

            return Results.Ok(responses);
        })
        .WithName("GetAlertResponses")
        .WithSummary("[Debug] Respuestas de una alerta, sin auth")
        .WithDescription(
            "Solo para debug manual — con webhooks configurados no debería hacer falta " +
            "consultar esto por polling, ver POST /api/webhooks.")
        .WithTags("Debug");
    }
}
