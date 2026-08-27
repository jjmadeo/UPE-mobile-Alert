using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MobileAlert.Api.Data;
using MobileAlert.Api.Endpoints;
using MobileAlert.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Configuración ----------------------------------------------------

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FcmOptions>(builder.Configuration.GetSection("Fcm"));
builder.Services.Configure<AlertRetryOptions>(builder.Configuration.GetSection("AlertRetry"));

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Falta Jwt:Secret en la configuración.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "mobile-alert-api";

// --- Servicios ----------------------------------------------------------

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sin esto, ASP.NET Core remapea "sub" a una claim URI larga
        // (herencia de WS-Federation) al validar el token, y
        // ClaimsPrincipalExtensions.GetFirefighterId (que busca "sub" tal
        // cual lo puso JwtTokenService) no lo encuentra.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    })
    // Segundo scheme, independiente del JWT de bomberos: lo usan los
    // backends de los cuarteles (header X-Api-Key) — ver
    // ApiKeyAuthenticationHandler.
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuth.SchemeName, null);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ApiKeyAuth.SchemeName, policy =>
        policy.AddAuthenticationSchemes(ApiKeyAuth.SchemeName).RequireAuthenticatedUser());

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<WebhookNotifier>();
builder.Services.AddSingleton<IFcmSender, FcmSender>();
builder.Services.AddHostedService<AlertRetryBackgroundService>();

// "login-delegation": le pegamos al backend PROPIO de cada cuartel con la
// contraseña real del bombero — timeout corto a propósito, para no dejar
// al bombero esperando el login si ese backend está caído.
builder.Services.AddHttpClient("login-delegation", c => c.Timeout = TimeSpan.FromSeconds(8));
// "webhooks": mismo criterio, no puede colgar el guardado de una respuesta.
builder.Services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(8));

builder.Services.AddCors(options =>
{
    // La app mobile no corre en un browser, pero mantenemos CORS abierto
    // para poder pegarle desde herramientas de prueba (curl no lo necesita,
    // pero un cliente HTTP en browser sí).
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// --- Migraciones + seed al arrancar --------------------------------------
// Práctico para dev (docker-compose up y ya está la base lista). En un
// pipeline de producción de verdad, esto se separaría a un paso explícito
// de deploy en vez de correr en cada arranque del proceso.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Logger);
}

// --- Pipeline -------------------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));
app.MapAuthEndpoints();
app.MapDevicesEndpoints();
app.MapAlertsEndpoints();
app.MapWebhooksEndpoints();

app.Run();

// Marker público para que WebApplicationFactory<Program> (backend/Tests)
// pueda hostear esta app en memoria — con top-level statements, el Program
// que genera el compilador es internal por defecto.
public partial class Program { }
