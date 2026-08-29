using System.Security.Claims;
using MobileAlert.Api.Dtos;
using MobileAlert.Api.Services;

namespace MobileAlert.Api.Endpoints;

public static class DevicesEndpoints
{
    public static void MapDevicesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/devices/register", async (
            RegisterDeviceRequestDto request,
            ClaimsPrincipal user,
            DeviceService devices,
            CancellationToken ct) =>
        {
            await devices.RegisterAsync(user.GetFirefighterId(), request.FcmToken, ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("RegisterDevice")
        .WithSummary("Registra (o reemplaza) el token FCM del dispositivo del bombero autenticado")
        .WithDescription(
            "Lo llama la APP MOBILE, con el JWT del login. El fan-out de alertas manda el push " +
            "a este token. Registrar uno nuevo para el mismo bombero borra el anterior — un " +
            "bombero tiene un solo dispositivo \"vigente\" a la vez.")
        .WithTags("Bombero (app mobile)");
    }
}
