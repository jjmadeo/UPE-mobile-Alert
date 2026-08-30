using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace MobileAlert.Api.Services;

public class FcmOptions
{
    public string? CredentialsPath { get; set; }
}

/// <summary>Pura para poder sustituirla en tests de integración (ver
/// backend/Tests) por un fake que registra qué se le mandó, sin depender de
/// credenciales reales de Firebase — así un test puede verificar "se
/// intentó mandar el push con estos datos" ejecutando la request HTTP real
/// de punta a punta, sin pegarle a FCM de verdad.</summary>
public interface IFcmSender
{
    Task<bool> SendAsync(string fcmToken, Dictionary<string, string> data, CancellationToken ct = default);

    /// <summary>Segundo push, en paralelo al data-only de <see cref="SendAsync"/>
    /// — con campo `notification`, para que Android lo muestre solo aunque
    /// el fabricante nunca deje correr el handler de la app (el caso que
    /// falla hoy en Samsung). Red de contención, no reemplazo: no dispara
    /// pantalla completa (ver AlertService.FanOutAsync y el comentario en
    /// displayAlertNotification.ts sobre por qué notification+data no
    /// llega al handler de background).</summary>
    Task<bool> SendFallbackNotificationAsync(
        string fcmToken, string title, string body, Dictionary<string, string> data, CancellationToken ct = default);
}

/// <summary>
/// Envuelve FirebaseAdmin.Messaging para mandar el mismo tipo de mensaje
/// "data-only" que hoy arma mock-server/send-test-alert.js a mano — sin
/// campo `notification`, para que siempre pase por el handler de JS del
/// lado de la app (ver el comentario en displayAlertNotification.ts sobre
/// por qué es a propósito).
///
/// Si no hay credenciales configuradas, no explota: loguea un warning y
/// las alertas se siguen creando/persistiendo, solo que sin push real —
/// mismo criterio que el optionalDependency de firebase-admin en el mock.
/// </summary>
public class FcmSender : IFcmSender
{
    private readonly ILogger<FcmSender> _logger;
    private readonly bool _enabled;

    public FcmSender(IOptions<FcmOptions> options, ILogger<FcmSender> logger)
    {
        _logger = logger;
        var path = options.Value.CredentialsPath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning(
                "FCM deshabilitado: no se encontró el archivo de credenciales ({Path}). " +
                "Las alertas se van a crear igual, pero no se manda ningún push real.",
                path);
            _enabled = false;
            return;
        }

        if (FirebaseApp.DefaultInstance is null)
        {
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) });
        }
        _enabled = true;
    }

    public async Task<bool> SendAsync(string fcmToken, Dictionary<string, string> data, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            return false;
        }

        var message = new Message
        {
            Token = fcmToken,
            Data = data,
            Android = new AndroidConfig { Priority = Priority.High },
            Apns = new ApnsConfig
            {
                Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                Aps = new Aps { ContentAvailable = true },
            },
        };

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning(ex, "Falló el envío FCM al token {Token}", fcmToken);
            return false;
        }
    }

    public async Task<bool> SendFallbackNotificationAsync(
        string fcmToken, string title, string body, Dictionary<string, string> data, CancellationToken ct = default)
    {
        if (!_enabled)
        {
            return false;
        }

        var message = new Message
        {
            Token = fcmToken,
            Notification = new Notification { Title = title, Body = body },
            Data = data,
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                // Mismo canal que crea la app en channel.ts (NOTIFICATION_CHANNEL_ID) —
                // tiene que coincidir a mano porque no hay forma de importar la
                // constante entre proyectos. Si el canal todavía no existe en el
                // teléfono (ensureAlertChannel no llegó a correr nunca), Android
                // igual muestra la notificación, solo que con la config default en
                // vez de la nuestra (sonido/vibración) — así que del lado de la app
                // conviene crear el canal apenas arranca, no recién al mostrar la
                // primera alerta (ver index.js).
                Notification = new AndroidNotification { ChannelId = "bomberos-alertas", Sound = "default" },
            },
            Apns = new ApnsConfig
            {
                Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                Aps = new Aps { Sound = "default" },
            },
        };

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogWarning(ex, "Falló el envío FCM (fallback notification) al token {Token}", fcmToken);
            return false;
        }
    }
}
