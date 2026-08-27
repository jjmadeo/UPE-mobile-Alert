using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;

namespace MobileAlert.Api.Services;

public class AlertRetryOptions
{
    public int IntervalSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 30;
}

/// <summary>
/// El reemplazo real del loop de reintento que hoy vive en
/// mock-server/send-test-alert.js (ver el comentario ahí: "esta misma idea
/// tiene que vivir en el backend, en el módulo de fan-out"). Cada
/// `IntervalSeconds`, revisa las alertas Pending y le reenvía el push a la
/// institución si todavía nadie respondió — hasta `MaxRetries` veces, y
/// deja de insistir apenas hay una respuesta (ver AlertService.RespondAsync)
/// o al llegar al máximo (la marca Expired).
/// </summary>
public class AlertRetryBackgroundService(
    IServiceScopeFactory scopeFactory,
    Microsoft.Extensions.Options.IOptions<AlertRetryOptions> options,
    ILogger<AlertRetryBackgroundService> logger
) : BackgroundService
{
    private readonly AlertRetryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Un error en un tick no puede tirar abajo el reintento de
                // TODAS las alertas futuras — se loguea y se sigue.
                logger.LogError(ex, "Error en el ciclo de reintento de alertas");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alertService = scope.ServiceProvider.GetRequiredService<AlertService>();

        var pending = await db.Alerts
            .Where(a => a.Status == AlertStatus.Pending)
            .ToListAsync(ct);

        foreach (var alert in pending)
        {
            if (alert.RetryCount >= _options.MaxRetries)
            {
                alert.Status = AlertStatus.Expired;
                logger.LogWarning(
                    "Alerta {AlertId} ({Title}) expiró sin respuesta tras {Retries} reintentos",
                    alert.Id, alert.Title, alert.RetryCount);
                continue;
            }

            await alertService.FanOutAsync(alert, ct);
            alert.RetryCount++;
            alert.LastSentAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
