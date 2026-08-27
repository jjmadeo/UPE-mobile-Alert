using Microsoft.EntityFrameworkCore;
using MobileAlert.Api.Data;
using MobileAlert.Api.Domain;

namespace MobileAlert.Api.Services;

public class DeviceService(AppDbContext db)
{
    /// <summary>Se llama en cada login (y cada vez que la app resincroniza
    /// el token — ver syncFcmTokenWithBackend del lado de la app). Compara
    /// el token recibido contra lo que YA teníamos guardado para ESTE
    /// bombero, y actualiza solo si cambió — así no se acumulan filas
    /// viejas muertas cada vez que FCM rota el token (pasa
    /// periódicamente).</summary>
    public async Task RegisterAsync(int firefighterId, string fcmToken, CancellationToken ct = default)
    {
        var ownedByThisFirefighter = await db.DeviceTokens
            .Where(d => d.FirefighterId == firefighterId)
            .ToListAsync(ct);

        var current = ownedByThisFirefighter.FirstOrDefault(d => d.FcmToken == fcmToken);
        if (current is not null)
        {
            // Mismo token que ya teníamos — nada que reemplazar.
            current.RegisteredAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        // Cambió (o no tenía ninguno todavía) — se sacan los que tenía
        // este bombero antes, se guarda el nuevo.
        db.DeviceTokens.RemoveRange(ownedByThisFirefighter);

        // Si el token nuevo ya estaba asociado a OTRO bombero (mismo
        // teléfono físico: cambió de cuenta, se reinstaló la app con otro
        // usuario, etc.) hay que sacárselo primero — un token FCM
        // pertenece a un solo dueño genuino a la vez, no tiene sentido que
        // el push de una alerta le llegue a dos bomberos distintos por el
        // mismo dispositivo.
        var stolenFrom = await db.DeviceTokens.FirstOrDefaultAsync(d => d.FcmToken == fcmToken, ct);
        if (stolenFrom is not null)
        {
            db.DeviceTokens.Remove(stolenFrom);
        }

        db.DeviceTokens.Add(new DeviceToken { FcmToken = fcmToken, FirefighterId = firefighterId });

        await db.SaveChangesAsync(ct);
    }
}
