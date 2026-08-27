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
        .WithName("RegisterDevice");
    }
}
