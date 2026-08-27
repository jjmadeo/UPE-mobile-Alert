using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MobileAlert.Api.Data;
using MobileAlert.Api.Dtos;
using Xunit;

namespace MobileAlert.Api.Tests;

/// <summary>
/// Tests de integración de punta a punta: cada uno dispara la request HTTP
/// real contra la API completa (auth, EF Core + Postgres real, todos los
/// endpoints tal cual los llama la app o el backend de un cuartel — ver
/// ApiFactory) y verifica el resultado "desde afuera", sin llamar a ningún
/// servicio interno directo. Lo único sustituido es IFcmSender (ver
/// FakeFcmSender) para no depender de credenciales reales de Firebase.
///
/// Comparten una sola ApiFactory (una sola Postgres de Testcontainers, un
/// solo seed) por costo — levantar un contenedor nuevo por test sería mucho
/// más lento. Por eso cada test usa tokens FCM / correlationIds propios
/// (Guid random) para no pisarse entre sí con el estado que va quedando.
/// </summary>
public class CriticalFlowsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string DemoApiKey = "demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev";

    // El servidor serializa/deserializa JSON en camelCase, case-insensitive
    // (default de ASP.NET Core para minimal APIs). HttpClient del lado
    // cliente NO usa esas mismas reglas por default (PostAsJsonAsync /
    // ReadFromJsonAsync sin opciones son case-SENSITIVE y PascalCase) — sin
    // esto, ReadFromJsonAsync<AlertCreatedResponseDto>() deserializaría
    // todo en blanco (Guid.Empty, 0, arrays vacíos) en vez de tirar error,
    // porque ningún nombre de propiedad calza.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private HttpClient CuartelClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", DemoApiKey);
        return client;
    }

    private async Task<(int FirefighterId, HttpClient Client)> LoginAsync(
        string username, string password = "1234", string institutionCode = "BOMBEROS-CENTRAL")
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync(
            "/api/auth/login", new { institutionCode, username, password });
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<LoginResponseDto>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        // FirefighterDto.Id viaja como string (ver comentario en
        // AuthService.LoginAsync) — se convierte acá, no del lado del DTO.
        return (int.Parse(login.Firefighter.Id), client);
    }

    private async Task<string> RegisterDeviceAsync(HttpClient appClient)
    {
        var fcmToken = $"fake-fcm-token-{Guid.NewGuid()}";
        var res = await appClient.PostAsJsonAsync("/api/devices/register", new { fcmToken });
        res.EnsureSuccessStatusCode();
        return fcmToken;
    }

    /// <summary>Acceso directo a la base para verificar estado que la API no
    /// expone en ninguna respuesta (qué tokens quedaron guardados, qué se
    /// persistió de la entrega de un webhook) — el resto de los tests
    /// verifican todo por HTTP nomás; estos puntualmente necesitan mirar
    /// "adentro". El scope hay que disponerlo — ahí vive el DbContext.</summary>
    private static AppDbContext GetDb(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task Login_ThenRegisterDevice_PersistsToken()
    {
        // maria/BOMBEROS-NORTE, no juan — para no compartir DeviceTokens
        // con el resto de los tests de esta clase (comparten una sola
        // Postgres, ver el comentario de la clase) y poder contar filas sin
        // ambigüedad.
        var (firefighterId, appClient) = await LoginAsync("maria", institutionCode: "BOMBEROS-NORTE");
        var fcmToken = await RegisterDeviceAsync(appClient);

        using var scope = factory.Services.CreateScope();
        var stored = await GetDb(scope).DeviceTokens.SingleAsync(d => d.FirefighterId == firefighterId);
        Assert.Equal(fcmToken, stored.FcmToken);
    }

    [Fact]
    public async Task RegisteringNewDevice_ReplacesPreviousToken()
    {
        // Mismo motivo que el test anterior para usar maria y no juan.
        var (firefighterId, appClient) = await LoginAsync("maria", institutionCode: "BOMBEROS-NORTE");

        // El bombero instala la app en un teléfono, después en otro (o
        // reinstala en el mismo) — FCM le da un token nuevo cada vez.
        var oldToken = await RegisterDeviceAsync(appClient);
        var newToken = await RegisterDeviceAsync(appClient);
        Assert.NotEqual(oldToken, newToken);

        // Un solo dispositivo "vigente" por bombero — el viejo se borra, no
        // queda acumulado (ver DeviceService.RegisterAsync). Importa de
        // verdad: si quedaran los dos, el fan-out le mandaría el push al
        // token viejo, que ya no sirve, además del nuevo.
        using var scope = factory.Services.CreateScope();
        var tokens = await GetDb(scope).DeviceTokens
            .Where(d => d.FirefighterId == firefighterId)
            .ToListAsync();
        var current = Assert.Single(tokens);
        Assert.Equal(newToken, current.FcmToken);
    }

    [Fact]
    public async Task CreateAlert_NotifiesRegisteredDevice_ViaFcm()
    {
        // Arrange: el bombero loguea y registra su device, como hace la app
        // en un arranque normal.
        var (firefighterId, appClient) = await LoginAsync("juan");
        var fcmToken = await RegisterDeviceAsync(appClient);
        var correlationId = Guid.NewGuid();

        // Act: el backend del cuartel dispara la alerta, autenticado con su
        // API key — mismo request que backend/scripts/send-test-alert.js.
        var request = new CreateAlertRequestDto(
            correlationId, "Incendio estructural", "Se solicita apoyo urgente",
            "Av. Siempre Viva 742", -32.89, -68.84, [firefighterId]);
        var response = await CuartelClient().PostAsJsonAsync("/api/alerts", request, JsonOptions);

        // Assert: 200, se reportó 1 dispositivo notificado y nada raro en
        // los desgloses de fallos parciales...
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<AlertCreatedResponseDto>(JsonOptions);
        Assert.Equal(1, created!.DevicesNotified);
        Assert.Empty(created.UnknownFirefighterIds);
        Assert.Empty(created.FirefightersWithoutDevice);

        // ...y lo que pide este test puntualmente: que el fan-out haya
        // llegado de verdad hasta el punto de intentar mandar el push por
        // FCM, con el token y los datos correctos — verificado por fuera,
        // sin llamar a AlertService.FanOutAsync a mano.
        var call = Assert.Single(factory.FakeFcm.Calls, c => c.FcmToken == fcmToken);
        Assert.Equal(correlationId.ToString(), call.Data["correlationId"]);
        Assert.Equal("Incendio estructural", call.Data["title"]);
        Assert.Equal("-32.89", call.Data["latitude"]);
    }

    [Fact]
    public async Task CreateAlert_SameCorrelationId_ReplaysIdempotently()
    {
        var (firefighterId, appClient) = await LoginAsync("juan");
        var fcmToken = await RegisterDeviceAsync(appClient);
        var correlationId = Guid.NewGuid();
        var cuartel = CuartelClient();
        var request = new CreateAlertRequestDto(
            correlationId, "Incendio estructural", "Se solicita apoyo urgente",
            "Av. Siempre Viva 742", null, null, [firefighterId]);

        // Act: la misma alerta (mismo correlationId) llega dos veces — pasa
        // de verdad si el backend del cuartel reintenta un POST que se
        // colgó, sin saber si el primero había llegado o no.
        var firstResponse = await cuartel.PostAsJsonAsync("/api/alerts", request, JsonOptions);
        var secondResponse = await cuartel.PostAsJsonAsync("/api/alerts", request, JsonOptions);

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<AlertCreatedResponseDto>(JsonOptions);
        var second = await secondResponse.Content.ReadFromJsonAsync<AlertCreatedResponseDto>(JsonOptions);

        // No se creó una alerta duplicada...
        Assert.Equal(first!.AlertId, second!.AlertId);

        // ...y, lo que de verdad importa para el bombero: no se le mandó el
        // push dos veces por el mismo aviso.
        var callsForThisAlert = factory.FakeFcm.Calls
            .Where(c => c.Data["correlationId"] == correlationId.ToString())
            .ToList();
        Assert.Single(callsForThisAlert);
        Assert.Equal(fcmToken, callsForThisAlert[0].FcmToken);
    }

    [Fact]
    public async Task RespondToAlert_DeliversWebhook_SignedWithCorrectSecret()
    {
        // Arrange: el cuartel registra su webhook...
        var cuartel = CuartelClient();
        var port = GetFreePort();
        var hookUrl = $"http://127.0.0.1:{port}/hook/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(hookUrl);
        listener.Start();
        var incomingRequest = listener.GetContextAsync();

        var webhookResponse = await cuartel.PostAsJsonAsync("/api/webhooks", new { url = hookUrl });
        webhookResponse.EnsureSuccessStatusCode();
        var webhook = await webhookResponse.Content.ReadFromJsonAsync<WebhookCreatedResponseDto>(JsonOptions);

        // ...el bombero loguea y le llega una alerta (juan, de
        // BOMBEROS-CENTRAL — la única institución con API key sembrada)...
        var (firefighterId, appClient) = await LoginAsync("juan");
        await RegisterDeviceAsync(appClient);
        var correlationId = Guid.NewGuid();
        var createResponse = await cuartel.PostAsJsonAsync("/api/alerts", new CreateAlertRequestDto(
            correlationId, "Incendio estructural", "Se solicita apoyo urgente", null, null, null, [firefighterId]), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<AlertCreatedResponseDto>(JsonOptions);

        // Act: el bombero responde. Ojo con el orden: NO se puede esperar
        // esta respuesta antes de atender al listener — RespondAsync espera
        // a que termine la llamada al webhook (ver AlertService.RespondAsync
        // / WebhookNotifier), así que hay que estar listo para contestarle
        // en paralelo, si no es un deadlock (esta request nunca completa
        // porque el webhook nunca recibe respuesta porque este mismo test
        // está bloqueado esperándola).
        var respondTask = appClient.PostAsJsonAsync(
            $"/api/alerts/{created!.AlertId}/response",
            new
            {
                alertId = created.AlertId.ToString(),
                response = "ATTENDING",
                location = (object?)null,
                respondedAt = DateTimeOffset.UtcNow.ToString("o"),
            });

        var ctx = await incomingRequest.WaitAsync(TimeSpan.FromSeconds(10));
        using var reader = new StreamReader(ctx.Request.InputStream);
        var payload = await reader.ReadToEndAsync();
        ctx.Response.StatusCode = 200;
        ctx.Response.OutputStream.Close();

        var respondResponse = await respondTask;
        respondResponse.EnsureSuccessStatusCode();

        // Assert: el webhook recibió el POST de verdad, firmado con el
        // secret que se devolvió al registrarlo (no uno inventado) — es la
        // garantía real que le vendemos al cuartel para que confíe en el
        // webhook sin auth adicional.

        var signature = ctx.Request.Headers["X-Signature"];
        Assert.False(string.IsNullOrEmpty(signature));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhook!.Secret));
        var expectedSignature = Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        Assert.Equal(expectedSignature, signature);

        Assert.Contains($"\"correlationId\":\"{correlationId}\"", payload);
        Assert.Contains("\"response\":\"ATTENDING\"", payload);

        // ...y también quedó la auditoría de la entrega en la base (url,
        // status code, y lo que el receptor contestó) — ver los campos
        // Webhook* de AlertResponseRecord. No alcanza con que el receptor
        // haya visto el POST: si esto no se persiste, no hay forma de
        // saber después "¿le llegó?" sin volver a preguntarle al cuartel.
        using var scope = factory.Services.CreateScope();
        var storedResponse = await GetDb(scope).AlertResponses
            .SingleAsync(r => r.AlertId == created.AlertId);
        Assert.Equal(hookUrl, storedResponse.WebhookUrl);
        Assert.Equal(200, storedResponse.WebhookStatusCode);
        // Comparación semántica, no de texto: la columna es jsonb (ver
        // AppDbContext), y Postgres normaliza el formato al guardarlo (por
        // ejemplo, agrega un espacio después de cada ":") — el string que
        // vuelve de la base nunca es byte a byte igual al que se mandó por
        // HTTP, aunque represente el mismo JSON.
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(payload),
            JsonNode.Parse(storedResponse.WebhookRequestPayload!)));
    }

    [Fact]
    public async Task RegisterWebhook_PersistsSubscription_WithSecretReturnedOnce()
    {
        var cuartel = CuartelClient();
        var url = $"https://cuartel-{Guid.NewGuid()}.example.org/hooks/mobile-alert";

        var response = await cuartel.PostAsJsonAsync("/api/webhooks", new { url });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<WebhookCreatedResponseDto>(JsonOptions);

        // El secret que devuelve la respuesta tiene que ser el mismo que
        // después firma cada entrega — si no calzaran, el cuartel no
        // podría verificar nunca ningún webhook (ver el otro test, que
        // valida la firma end to end contra ESTE valor).
        Assert.False(string.IsNullOrWhiteSpace(created!.Secret));
        Assert.Equal(url, created.Url);

        using var scope = factory.Services.CreateScope();
        var stored = await GetDb(scope).Webhooks.SingleAsync(w => w.Id == created.Id);
        Assert.Equal(url, stored.Url);
        Assert.Equal(created.Secret, stored.Secret);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task RegisterWebhook_RejectsInvalidUrl()
    {
        var response = await CuartelClient().PostAsJsonAsync("/api/webhooks", new { url = "no-es-una-url" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
