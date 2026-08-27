namespace MobileAlert.Api.Dtos;

public record CreateWebhookRequestDto(string Url);

/// <summary>El Secret solo se devuelve UNA VEZ, en esta respuesta — no hay
/// forma de volver a consultarlo después (guardamos solo lo necesario para
/// firmar, no hace falta guardarlo en texto claro tampoco, pero por ahora
/// se guarda así — ver el comentario en WebhookSubscription).</summary>
public record WebhookCreatedResponseDto(int Id, string Url, string Secret);
