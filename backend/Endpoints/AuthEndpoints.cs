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
        .WithName("Login")
        .WithSummary("Login de un bombero")
        .WithDescription(
            "Lo llama la APP MOBILE, no el backend del cuartel. Valida usuario/institución " +
            "y devuelve un JWT (usarlo como Bearer en el resto de los endpoints de bombero) " +
            "más el branding de la institución. Si la institución tiene LoginBackendUrl " +
            "configurado, la contraseña se reenvía tal cual a ese sistema propio; si no, se " +
            "valida localmente (BCrypt).")
        .WithTags("Bombero (app mobile)");
    }
}
