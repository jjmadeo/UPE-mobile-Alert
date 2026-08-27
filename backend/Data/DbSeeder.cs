using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Services;

namespace MobileAlert.Api.Data;

/// <summary>
/// Mismos datos de prueba que mock-server/server.js (mismas instituciones,
/// mismos usuarios/contraseñas) para que se pueda migrar de un backend al
/// otro sin tener que recordar credenciales nuevas — la diferencia es que
/// acá la contraseña se guarda hasheada (BCrypt), no en texto plano.
/// </summary>
public static class DbSeeder
{
    // Fija a propósito (no generada al azar) para que sea la misma en cada
    // `docker compose up` limpio y quede documentada acá — es SOLO para
    // desarrollo local. Una API key real de un cuartel se genera aparte y
    // se carga a mano (ver README del backend).
    private const string DemoApiKey = "demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev";

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Institutions.AnyAsync())
        {
            return;
        }

        var central = new Institution
        {
            Code = "BOMBEROS-CENTRAL",
            Name = "Bomberos Voluntarios Central",
            PrimaryColor = "#1E3A8A",
        };
        var norte = new Institution
        {
            Code = "BOMBEROS-NORTE",
            Name = "Bomberos Voluntarios Zona Norte",
            PrimaryColor = "#B45309",
        };
        db.Institutions.AddRange(central, norte);

        db.Firefighters.AddRange(
            new Firefighter
            {
                Name = "Juan Pérez",
                Username = "juan",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
                Institution = central,
            },
            new Firefighter
            {
                Name = "María Gómez",
                Username = "maria",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("1234"),
                Institution = norte,
            }
        );

        db.ApiKeys.Add(new ApiKeyRecord
        {
            Name = "Demo — dev local",
            KeyHash = ApiKeyAuth.Hash(DemoApiKey),
            Institution = central,
        });

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seed inicial cargado. API key de prueba para BOMBEROS-CENTRAL (header X-Api-Key): {Key}",
            DemoApiKey);
    }
}
