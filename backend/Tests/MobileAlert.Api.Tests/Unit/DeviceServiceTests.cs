using System.Linq;
using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Services;
using Xunit;

namespace MobileAlert.Api.Tests.Unit;

/// <summary>DeviceService.RegisterAsync es lo que decide a qué token FCM le
/// llega el push de una alerta — si estas reglas se rompen, un bombero deja
/// de recibir avisos (token viejo que nadie borró) o le llegan a alguien que
/// ya no es dueño de ese teléfono.</summary>
public class DeviceServiceTests
{
    [Fact]
    public async Task RegisterAsync_NewFirefighter_CreatesDeviceToken()
    {
        await using var db = InMemoryDb.New();
        var service = new DeviceService(db);

        await service.RegisterAsync(firefighterId: 1, fcmToken: "token-a");

        var stored = await db.DeviceTokens.SingleAsync();
        Assert.Equal(1, stored.FirefighterId);
        Assert.Equal("token-a", stored.FcmToken);
    }

    [Fact]
    public async Task RegisterAsync_SameTokenAgain_RefreshesRegisteredAt_WithoutDuplicating()
    {
        await using var db = InMemoryDb.New();
        var service = new DeviceService(db);
        await service.RegisterAsync(1, "token-a");
        var firstRegisteredAt = (await db.DeviceTokens.SingleAsync()).RegisteredAt;

        await Task.Delay(10);
        await service.RegisterAsync(1, "token-a");

        var current = await db.DeviceTokens.SingleAsync();
        Assert.True(current.RegisteredAt > firstRegisteredAt);
    }

    [Fact]
    public async Task RegisterAsync_NewToken_RemovesFirefightersPreviousToken()
    {
        await using var db = InMemoryDb.New();
        var service = new DeviceService(db);
        await service.RegisterAsync(1, "token-viejo");

        await service.RegisterAsync(1, "token-nuevo");

        // Un solo dispositivo "vigente" por bombero — el fan-out
        // (AlertService.FanOutAsync) le manda el push a TODOS los tokens
        // que encuentre para el firefighterId; si el viejo quedara, se le
        // mandaría a un token que FCM ya considera inválido.
        var tokens = await db.DeviceTokens.Where(d => d.FirefighterId == 1).ToListAsync();
        var current = Assert.Single(tokens);
        Assert.Equal("token-nuevo", current.FcmToken);
    }

    [Fact]
    public async Task RegisterAsync_TokenPreviouslyOwnedByAnotherFirefighter_IsReassigned()
    {
        // Mismo teléfono físico, dos bomberos distintos en momentos
        // distintos (se reinstaló la app con otra cuenta, por ejemplo) — el
        // token FCM viejo no puede quedar apuntando al bombero anterior, si
        // no una alerta para el bombero 2 le llegaría también al 1, que ya
        // no usa ese teléfono.
        await using var db = InMemoryDb.New();
        var service = new DeviceService(db);
        await service.RegisterAsync(firefighterId: 1, fcmToken: "telefono-compartido");

        await service.RegisterAsync(firefighterId: 2, fcmToken: "telefono-compartido");

        var owner = await db.DeviceTokens.SingleAsync(d => d.FcmToken == "telefono-compartido");
        Assert.Equal(2, owner.FirefighterId);
        Assert.Empty(await db.DeviceTokens.Where(d => d.FirefighterId == 1).ToListAsync());
    }
}
