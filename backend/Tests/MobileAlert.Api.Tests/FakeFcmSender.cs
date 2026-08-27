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
    private readonly object _lock = new();

    public IReadOnlyList<Call> Calls
    {
        get { lock (_lock) { return _calls.ToList(); } }
    }

    public Task<bool> SendAsync(string fcmToken, Dictionary<string, string> data, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _calls.Add(new Call(fcmToken, data));
        }
        return Task.FromResult(true);
    }
}
