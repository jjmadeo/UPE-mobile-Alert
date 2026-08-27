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
}
