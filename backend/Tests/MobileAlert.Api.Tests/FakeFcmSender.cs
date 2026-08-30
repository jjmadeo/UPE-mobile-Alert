using MobileAlert.Api.Services;

namespace MobileAlert.Api.Tests;

/// <summary>Reemplaza a <see cref="FcmSender"/> (que necesita credenciales
/// reales de Firebase) en los tests de integración — registra cada llamada
/// para que un test pueda verificar "se intentó mandar el push, con este
/// token y estos datos", ejecutando la request HTTP real de punta a punta
/// (ver ApiFactory), sin depender de que FCM esté configurado.</summary>
public class FakeFcmSender : IFcmSender
{
    public record Call(string FcmToken, Dictionary<string, string> Data);

    private readonly List<Call> _calls = [];
    private readonly List<Call> _fallbackCalls = [];
    private readonly object _lock = new();

    /// <summary>Demora artificial antes de "responder" cada envío — sirve
    /// para simular la latencia real de pegarle a Firebase y así poder
    /// probar que AlertService.FanOutAsync manda en paralelo entre
    /// dispositivos, no uno atrás del otro (ver
    /// AlertServiceTests.FanOutAsync_SendsToManyDevices_InParallel).
    /// Cero por defecto: no afecta a ningún otro test.</summary>
    public TimeSpan SendDelay { get; set; } = TimeSpan.Zero;

    public IReadOnlyList<Call> Calls
    {
        get { lock (_lock) { return _calls.ToList(); } }
    }

    /// <summary>Llamadas a <see cref="SendFallbackNotificationAsync"/> (la
    /// red de contención en paralelo — ver AlertService.FanOutAsync),
    /// separadas de <see cref="Calls"/> para que un test pueda verificar
    /// una sin mezclarla con la otra.</summary>
    public IReadOnlyList<Call> FallbackCalls
    {
        get { lock (_lock) { return _fallbackCalls.ToList(); } }
    }

    public async Task<bool> SendAsync(string fcmToken, Dictionary<string, string> data, CancellationToken ct = default)
    {
        if (SendDelay > TimeSpan.Zero)
        {
            await Task.Delay(SendDelay, ct);
        }
        lock (_lock)
        {
            _calls.Add(new Call(fcmToken, data));
        }
        return true;
    }

    public async Task<bool> SendFallbackNotificationAsync(
        string fcmToken, string title, string body, Dictionary<string, string> data, CancellationToken ct = default)
    {
        if (SendDelay > TimeSpan.Zero)
        {
            await Task.Delay(SendDelay, ct);
        }
        lock (_lock)
        {
            _fallbackCalls.Add(new Call(fcmToken, data));
        }
        return true;
    }
}
