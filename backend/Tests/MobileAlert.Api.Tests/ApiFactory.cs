using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileAlert.Api.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace MobileAlert.Api.Tests;

/// <summary>
/// Hostea la API real (Program.cs completo: migraciones, seed, todos los
/// endpoints) en memoria, contra una Postgres real de verdad — no el
/// proveedor InMemory de EF Core, que no entiende `jsonb` ni columnas
/// `int[]` nativas de Postgres (justo lo que usan AlertRecord y
/// AlertResponseRecord) y hubiera dejado sin probar la parte más propensa a
/// romperse. La Postgres la levanta Testcontainers, un contenedor Docker
/// nuevo por corrida de tests — funciona "Docker fuera de Docker" (el
/// contenedor del SDK que corre `dotnet test` habla con el socket de Docker
/// del host, ver Tests/README.md) sin instalar nada en la máquina.
///
/// Lo único que se reemplaza de la app real es <see cref="IFcmSender"/> —
/// por <see cref="FakeFcmSender"/>, que registra qué se le mandó en vez de
/// pegarle a Firebase de verdad — así un test puede disparar la request
/// HTTP real de punta a punta y verificar "se intentó mandar el push, con
/// estos datos" sin necesitar credenciales de Firebase.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("mobilealert")
        .WithUsername("mobilealert")
        .WithPassword("mobilealert")
        .Build();

    public FakeFcmSender FakeFcm { get; } = new();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                // Bien alto — no queremos que AlertRetryBackgroundService
                // reintente en medio de un test y meta una llamada a FCM
                // extra que rompa un assert de "se llamó una sola vez".
                ["AlertRetry:IntervalSeconds"] = "3600",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IFcmSender>();
            services.AddSingleton<IFcmSender>(FakeFcm);
        });
    }
}
