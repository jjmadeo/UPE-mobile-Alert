using MobileAlert.Api.Dtos;
using MobileAlert.Api.Services;

namespace MobileAlert.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (LoginRequestDto request, AuthService auth, CancellationToken ct) =>
        {
            try
            {
                var result = await auth.LoginAsync(request, ct);
                return Results.Ok(result);
            }
            catch (InvalidLoginException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .WithName("Login");
    }
}
