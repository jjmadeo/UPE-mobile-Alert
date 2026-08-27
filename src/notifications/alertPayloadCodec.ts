import { AlertPayload } from '../types/alert';

/**
 * FCM manda todos los campos de `data` como string, y notifee guarda `data`
 * como Record<string, string | number | object>. Estas funciones centralizan
 * la conversión ida y vuelta para no repetir parseo suelto por todos lados.
 */

export function alertToNotifeeData(
  alert: AlertPayload,
): Record<string, string> {
  return {
    alertId: alert.id,
    correlationId: alert.correlationId ?? '',
    title: alert.title,
    message: alert.message,
    address: alert.address ?? '',
    // FCM data solo admite strings — lat/lng viajan como texto y se
    // parsean de vuelta en alertFromData. Ausentes (no ambas presentes) si
    // el backend no mandó ubicación del siniestro.
    latitude: alert.latitude !== undefined ? String(alert.latitude) : '',
    longitude: alert.longitude !== undefined ? String(alert.longitude) : '',
    createdAt: alert.createdAt,
  };
}

export function alertFromData(
  data: Record<string, unknown> | undefined,
): AlertPayload | null {
  if (!data || typeof data.alertId !== 'string') {
    return null;
  }

  const latitude =
    typeof data.latitude === 'string' && data.latitude !== ''
      ? Number(data.latitude)
      : undefined;
  const longitude =
    typeof data.longitude === 'string' && data.longitude !== ''
      ? Number(data.longitude)
      : undefined;

  return {
    id: data.alertId,
    correlationId:
      typeof data.correlationId === 'string' && data.correlationId
        ? data.correlationId
        : undefined,
    title: typeof data.title === 'string' ? data.title : 'Alerta',
    message: typeof data.message === 'string' ? data.message : '',
    address:
      typeof data.address === 'string' && data.address
        ? data.address
        : undefined,
    // Solo se exponen si las dos son números válidos — una coordenada
    // suelta no sirve para calcular distancia.
    latitude:
      latitude !== undefined && !Number.isNaN(latitude) ? latitude : undefined,
    longitude:
      longitude !== undefined && !Number.isNaN(longitude)
        ? longitude
        : undefined,
    createdAt:
      typeof data.createdAt === 'string'
        ? data.createdAt
        : new Date().toISOString(),
  };
}
