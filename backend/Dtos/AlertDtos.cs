namespace MobileAlert.Api.Dtos;

public record LocationDto(double Latitude, double Longitude, double? Accuracy, long Timestamp);

/// <summary>response llega como "ATTENDING"/"NOT_ATTENDING" — mismo string
/// que AlertResponseType del lado de la app (src/types/alert.ts). Se
/// convierte a mano en AlertsEndpoints en vez de mapear el enum directo por
/// JSON, para no atarse a que System.Text.Json entienda el casing.</summary>
public record RespondAlertRequestDto(
    string AlertId,
    string Response,
    LocationDto? Location,
    string RespondedAt
);

/// <summary>Body para crear y disparar una alerta nueva a un conjunto
/// explícito de bomberos — el equivalente real de correr
/// mock-server/send-test-alert.js a mano. A qué INSTITUCIÓN pertenece la
/// alerta lo determina la API key del request (header X-Api-Key), no un
/// campo acá; a qué BOMBEROS puntuales, `FirefighterIds` — ver
/// AlertsEndpoints.
///
/// `FirefighterIds` son nuestros ids internos (los que devuelve
/// `Firefighter.Id` — el mismo `firefighter.id` que la app recibe en el
/// login). El cuartel los tiene que haber visto antes de alguna forma: por
/// ejemplo, vía su propio backend de login delegado (ver
/// AuthService.LoginDelegatedAsync) — nosotros no exponemos un endpoint
/// para "listar bomberos" todavía.
///
/// `CorrelationId` lo genera y elige el backend del cuartel, no nosotros
/// (a diferencia de AlertId, que es nuestra PK interna) — viaja tal cual
/// hasta el dispositivo y vuelve en el webhook de cada respuesta (ver
/// WebhookNotifier). Como es un UUID de alta entropía que solo conoce el
/// cuartel que lo generó, funciona como prueba implícita de que un webhook
/// entrante corresponde de verdad a un aviso propio — su endpoint de
/// recepción de webhooks puede ser público sin auth adicional, porque
/// nadie puede adivinar o falsificar el correlationId correcto.</summary>
public record CreateAlertRequestDto(
    Guid CorrelationId,
    string Title,
    string Message,
    string? Address,
    double? Latitude,
    double? Longitude,
    int[] FirefighterIds
);

/// <summary>`UnknownFirefighterIds`: ids que no existen en ESTA institución
/// (nunca se logueó ese id, o pertenece a otra institución — no
/// distinguimos, para no filtrar información de otras instituciones).
/// `FirefightersWithoutDevice`: existen, pero no tienen ningún
/// DeviceToken registrado todavía (nunca abrieron la app / nunca dieron
/// permiso de notificaciones). Ambas listas pueden venir no vacías incluso
/// en una respuesta 200 — significa "se mandó a los que se pudo, pero
/// ojo con estos otros".</summary>
public record AlertCreatedResponseDto(
    Guid AlertId,
    Guid CorrelationId,
    int DevicesNotified,
    int[] UnknownFirefighterIds,
    int[] FirefightersWithoutDevice
);
