using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;
using MobileAlert.Api.Dtos;
using MobileAlert.Api.Services;
using Xunit;

namespace MobileAlert.Api.Tests.Unit;

/// <summary>Cubre la lógica de negocio de AlertService que no necesita
/// Postgres real para probarse (fan-out condicionado a tener device,
/// aislamiento multi-institución, upsert de respuestas) — ver
/// CriticalFlowsTests para el mismo servicio ejercitado de punta a punta por
/// HTTP contra Postgres real.</summary>
public class AlertServiceTests
{
    /// <summary>No debería llamarse nunca en estos tests — ninguno registra
    /// un WebhookSubscription, así que WebhookNotifier.NotifyResponseAsync
    /// tiene que cortar apenas ve que no hay webhooks activos, antes de
    /// pedir un HttpClient. Si algún día un test agrega un webhook y esto
    /// explota, es la señal de que hay que reemplazar esto por un fake de
    /// verdad, no ignorarlo.</summary>
    private sealed class ThrowingHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException(
                "No debería pedirse un HttpClient — no hay webhooks activos en este test.");
    }

    private static AlertService BuildService(AppDbContext db, IFcmSender fcm) =>
        new(db, fcm,
            new WebhookNotifier(new ThrowingHttpClientFactory(), db, NullLogger<WebhookNotifier>.Instance),
            NullLogger<AlertService>.Instance);

    private static async Task<(Institution Institution, Institution OtherInstitution)> SeedInstitutionsAsync(AppDbContext db)
    {
        var institution = new Institution { Code = "TEST-CENTRAL", Name = "Test Central", PrimaryColor = "#000000" };
        var other = new Institution { Code = "TEST-OTRA", Name = "Test Otra", PrimaryColor = "#111111" };
        db.Institutions.AddRange(institution, other);
        await db.SaveChangesAsync();
        return (institution, other);
    }

    [Fact]
    public async Task CreateAsync_MixedTargets_NotifiesOnlyFirefightersWithDevice_AndReportsTheRest()
    {
        await using var db = InMemoryDb.New();
        var (institution, otherInstitution) = await SeedInstitutionsAsync(db);

        var withDevice = new Firefighter { Name = "Con device", Username = "a", InstitutionId = institution.Id };
        var withoutDevice = new Firefighter { Name = "Sin device", Username = "b", InstitutionId = institution.Id };
        // Existe de verdad, pero en OTRA institución — clave del test de
        // aislamiento de abajo.
        var fromOtherInstitution = new Firefighter { Name = "Ajeno", Username = "c", InstitutionId = otherInstitution.Id };
        db.Firefighters.AddRange(withDevice, withoutDevice, fromOtherInstitution);
        await db.SaveChangesAsync();

        db.DeviceTokens.Add(new DeviceToken { FcmToken = "token-real", FirefighterId = withDevice.Id });
        await db.SaveChangesAsync();

        var fakeFcm = new FakeFcmSender();
        var service = BuildService(db, fakeFcm);
        var request = new CreateAlertRequestDto(
            Guid.NewGuid(), "Incendio", "msg", null, null, null,
            [withDevice.Id, withoutDevice.Id, fromOtherInstitution.Id]);

        var result = await service.CreateAsync(institution.Id, request);

        Assert.Equal(1, result.DevicesNotified);
        Assert.Equal([withDevice.Id], result.Alert.TargetFirefighterIds);
        Assert.Equal([withoutDevice.Id], result.FirefightersWithoutDevice);
        var call = Assert.Single(fakeFcm.Calls);
        Assert.Equal("token-real", call.FcmToken);

        // El de otra institución se reporta como "unknown" — igual que un
        // id que directamente no existe en ningún lado. Adrede: si dijera
        // "existe pero es de otra institución" en vez de "no existe",
        // filtraría que ese id es válido EN ALGÚN LADO, y una API key de un
        // cuartel no tiene por qué poder confirmar eso de otro cuartel.
        Assert.Equal([fromOtherInstitution.Id], result.UnknownFirefighterIds);
    }

    [Fact]
    public async Task CreateAsync_NoValidTargets_StillCreatesTheAlert_WithZeroDevicesNotified()
    {
        // Se prioriza dejar constancia aunque nadie vaya a recibir nada —
        // ver el comentario en AlertService.CreateAsync sobre por qué esto
        // NO es un error de request.
        await using var db = InMemoryDb.New();
        var (institution, _) = await SeedInstitutionsAsync(db);
        var service = BuildService(db, new FakeFcmSender());

        var result = await service.CreateAsync(institution.Id, new CreateAlertRequestDto(
            Guid.NewGuid(), "Incendio", "msg", null, null, null, [999]));

        Assert.Equal(0, result.DevicesNotified);
        Assert.Equal([999], result.UnknownFirefighterIds);
        Assert.NotEqual(Guid.Empty, result.Alert.Id);
    }

    [Fact]
    public async Task CreateAsync_SameCorrelationId_DoesNotFanOutTwice()
    {
        await using var db = InMemoryDb.New();
        var (institution, _) = await SeedInstitutionsAsync(db);
        var firefighter = new Firefighter { Name = "F", Username = "f", InstitutionId = institution.Id };
        db.Firefighters.Add(firefighter);
        await db.SaveChangesAsync();
        db.DeviceTokens.Add(new DeviceToken { FcmToken = "token", FirefighterId = firefighter.Id });
        await db.SaveChangesAsync();

        var fakeFcm = new FakeFcmSender();
        var service = BuildService(db, fakeFcm);
        var request = new CreateAlertRequestDto(
            Guid.NewGuid(), "Incendio", "msg", null, null, null, [firefighter.Id]);

        var first = await service.CreateAsync(institution.Id, request);
        var second = await service.CreateAsync(institution.Id, request);

        Assert.Equal(first.Alert.Id, second.Alert.Id);
        Assert.Single(fakeFcm.Calls);
        Assert.Single(await db.Alerts.ToListAsync());
    }

    [Fact]
    public async Task GetUnansweredFirefighterIdsAsync_ExcludesThoseWhoAlreadyResponded()
    {
        await using var db = InMemoryDb.New();
        var (institution, _) = await SeedInstitutionsAsync(db);
        var f1 = new Firefighter { Name = "F1", Username = "f1", InstitutionId = institution.Id };
        var f2 = new Firefighter { Name = "F2", Username = "f2", InstitutionId = institution.Id };
        var f3 = new Firefighter { Name = "F3", Username = "f3", InstitutionId = institution.Id };
        db.Firefighters.AddRange(f1, f2, f3);
        await db.SaveChangesAsync();

        var alert = new AlertRecord
        {
            CorrelationId = Guid.NewGuid(),
            Title = "t",
            Message = "m",
            InstitutionId = institution.Id,
            TargetFirefighterIds = [f1.Id, f2.Id, f3.Id],
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();
        db.AlertResponses.Add(new AlertResponseRecord
        {
            AlertId = alert.Id,
            FirefighterId = f2.Id,
            Response = AlertResponseType.Attending,
        });
        await db.SaveChangesAsync();

        var service = BuildService(db, new FakeFcmSender());
        var unanswered = await service.GetUnansweredFirefighterIdsAsync(alert.Id);

        Assert.Equal([f1.Id, f3.Id], unanswered.OrderBy(id => id));
    }

    [Fact]
    public async Task RespondAsync_SecondResponseFromSameFirefighter_UpdatesInPlace_NotDuplicate()
    {
        await using var db = InMemoryDb.New();
        var (institution, _) = await SeedInstitutionsAsync(db);
        var firefighter = new Firefighter { Name = "F", Username = "f", InstitutionId = institution.Id };
        db.Firefighters.Add(firefighter);
        await db.SaveChangesAsync();

        var alert = new AlertRecord
        {
            CorrelationId = Guid.NewGuid(),
            Title = "t",
            Message = "m",
            InstitutionId = institution.Id,
            TargetFirefighterIds = [firefighter.Id],
        };
        db.Alerts.Add(alert);
        await db.SaveChangesAsync();

        var service = BuildService(db, new FakeFcmSender());

        // Se arrepiente: primero dice que va, después que no.
        await service.RespondAsync(firefighter.Id, new RespondAlertRequestDto(
            alert.Id.ToString(), "ATTENDING", null, DateTimeOffset.UtcNow.ToString("o")));
        await service.RespondAsync(firefighter.Id, new RespondAlertRequestDto(
            alert.Id.ToString(), "NOT_ATTENDING", null, DateTimeOffset.UtcNow.ToString("o")));

        var responses = await db.AlertResponses.Where(r => r.AlertId == alert.Id).ToListAsync();
        var current = Assert.Single(responses);
        Assert.Equal(AlertResponseType.NotAttending, current.Response);

        var reloaded = await db.Alerts.SingleAsync(a => a.Id == alert.Id);
        Assert.Equal(AlertStatus.Answered, reloaded.Status);
    }

    [Fact]
    public async Task RespondAsync_UnknownAlertId_ThrowsKeyNotFound()
    {
        await using var db = InMemoryDb.New();
        var service = BuildService(db, new FakeFcmSender());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RespondAsync(1, new RespondAlertRequestDto(
            Guid.NewGuid().ToString(), "ATTENDING", null, DateTimeOffset.UtcNow.ToString("o"))));
    }
}
