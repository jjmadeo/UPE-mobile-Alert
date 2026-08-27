using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;

namespace MobileAlert.Api.Services;

/// <summary>
/// Le avisa a los webhooks registrados de una institución cada vez que un
/// bombero responde una alerta — el reemplazo real del polling que hoy
/// hace mock-server/send-test-alert.js contra
/// GET /api/alerts/:id/responses.
///
/// Cada request va firmado (header X-Signature, HMAC-SHA256 sobre el body
/// con el secreto de esa suscripción) para que el cuartel pueda verificar
/// que el webhook vino realmente de acá.
/// </summary>
public class WebhookNotifier(
    IHttpClientFactory httpClientFactory,
    AppDbContext db,
    ILogger<WebhookNotifier> logger)
{
    public async Task NotifyResponseAsync(AlertRecord alert, AlertResponseRecord response, CancellationToken ct = default)
    {
        var webhooks = await db.Webhooks
            .Where(w => w.InstitutionId == alert.InstitutionId && w.IsActive)
            .ToListAsync(ct);

        if (webhooks.Count == 0)
        {
            return;
        }

        var payload = new
        {
            alertId = alert.Id,
            // El cuartel identifica DE QUÉ aviso propio es esta respuesta
            // por este campo, no por alertId (que es nuestra PK interna,
            // no la de ellos) — ver el comentario en CreateAlertRequestDto.
            correlationId = alert.CorrelationId,
            firefighterId = response.FirefighterId,
            response = response.Response == AlertResponseType.Attending ? "ATTENDING" : "NOT_ATTENDING",
            location = response.Latitude is not null
                ? new { latitude = response.Latitude, longitude = response.Longitude, accuracy = response.Accuracy }
                : null,
            respondedAt = response.RespondedAt,
        };
        var json = JsonSerializer.Serialize(payload);
        var client = httpClientFactory.CreateClient("webhooks");

        // Auditoría de la entrega — ver los campos Webhook* en
        // AlertResponseRecord. Si hay más de un webhook activo (hoy no es
        // el caso típico), esto solo se queda con el resultado del
        // ÚLTIMO intentado; ver el comentario ahí sobre esa limitación
        // aceptada a propósito.
        response.WebhookRequestPayload = json;

        foreach (var webhook in webhooks)
        {
            response.WebhookUrl = webhook.Url;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-Signature", Sign(json, webhook.Secret));

                var httpResponse = await client.SendAsync(request, ct);
                response.WebhookStatusCode = (int)httpResponse.StatusCode;
                response.WebhookResponsePayload = await httpResponse.Content.ReadAsStringAsync(ct);

                if (!httpResponse.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Webhook {Url} (institución {InstitutionId}) respondió {Status}",
                        webhook.Url, webhook.InstitutionId, httpResponse.StatusCode);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Un webhook caído (o que no contesta a tiempo) no puede
                // tirar abajo el guardado de la respuesta del bombero — ya
                // se persistió antes de llegar acá (ver
                // AlertService.RespondAsync). Se loguea, se deja constancia
                // (sin status code — nunca hubo respuesta HTTP) y se sigue
                // con el resto de los webhooks, si hay más de uno.
                //
                // El guard es "!ct.IsCancellationRequested", NO "ex is not
                // OperationCanceledException" — un webhook que tarda más de
                // lo que dura el timeout del HttpClient ("webhooks", 8s en
                // Program.cs) también lanza OperationCanceledException (una
                // TaskCanceledException), y con el guard viejo eso se
                // colaba sin capturar y tiraba abajo TODA la respuesta del
                // bombero con un 500 — exactamente lo que este catch dice
                // que no tiene que pasar. Detectado por
                // Tests/.../CriticalFlowsTests.RespondToAlert_*.
                response.WebhookStatusCode = null;
                response.WebhookResponsePayload = $"[sin respuesta] {ex.GetType().Name}: {ex.Message}";
                logger.LogWarning(ex, "Falló el webhook a {Url}", webhook.Url);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
