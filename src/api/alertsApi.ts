import { getApiClient } from './client';
import {
  AlertResponsePayload,
  AlertResponseType,
  DeviceLocation,
} from '../types/alert';

/**
 * Envía la confirmación/rechazo del bombero al backend. Se usa tanto para
 * "Asistir" como para "No asistir": el backend registra ambas respuestas
 * por igual, la diferencia es solo el valor de `response`.
 */
export async function respondToAlert(
  alertId: string,
  response: AlertResponseType,
  location: DeviceLocation | null,
): Promise<void> {
  const payload: AlertResponsePayload = {
    alertId,
    response,
    location,
    respondedAt: new Date().toISOString(),
  };
  await getApiClient().post(`/api/alerts/${alertId}/response`, payload);
}
