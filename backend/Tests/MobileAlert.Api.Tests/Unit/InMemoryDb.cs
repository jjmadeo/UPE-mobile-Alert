using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;

namespace MobileAlert.Api.Tests.Unit;

/// <summary>
/// Estos tests NO usan Testcontainers/Postgres (ver CriticalFlowsTests para
/// esos) — son de la lógica de un servicio suelto, contra una base en
/// memoria nueva por test (nombre random, así uno no ve datos de otro). Vale
/// la pena la diferencia con Postgres real porque nada de lo que se prueba
/// acá depende de comportamiento específico de Postgres (jsonb, arrays
/// nativos vía `@>`/`ANY`) — son índices únicos, relaciones y reglas de
/// negocio comunes, que EF Core InMemory respeta igual.
/// </summary>
internal static class InMemoryDb
{
    public static AppDbContext New() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
