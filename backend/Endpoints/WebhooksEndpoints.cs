using System.Security.Cryptography;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Dtos;
using MobileAlert.Api.Services;

namespace MobileAlert.Api.Endpoints;

public static class WebhooksEndpoints
{
    public static void MapWebhooksEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks", async (
            CreateWebhookRequestDto request,
            System.Security.Claims.ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
            {
                return Results.BadRequest(new { message = "Url inválida — tiene que ser http(s) absoluta." });
            }

            var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

            var webhook = new WebhookSubscription
            {
                InstitutionId = user.GetInstitutionId(),
                Url = request.Url,
                Secret = secret,
            };
            db.Webhooks.Add(webhook);
            await db.SaveChangesAsync(ct);

            // El secreto se devuelve UNA sola vez, acá — no queda forma de
            // volver a consultarlo después (ver comentario en el DTO).
            return Results.Ok(new WebhookCreatedResponseDto(webhook.Id, webhook.Url, secret));
        })
        .RequireAuthorization(ApiKeyAuth.SchemeName)
        .WithName("CreateWebhook");
    }
}
