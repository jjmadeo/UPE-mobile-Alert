/**
 * Forma del aviso que dispara la app de bomberos.
 *
 * Llega como "data message" de FCM (no como notification message), para que
 * el JS tenga control total de cómo se muestra (pantalla roja + full screen
 * intent) incluso con la app cerrada o el teléfono bloqueado.
 */
export interface AlertPayload {
  /** Id único del aviso, generado por el backend. Se usa para responder. */
  id: string;
  /** Id generado por el backend del CUARTEL (no por nuestro backend) —
   * viaja sin usarse del lado de la app, pero vuelve en el webhook que
   * nuestro backend le manda al cuartel al responder, para que el cuartel
   * sepa a cuál de sus propios avisos corresponde (ver
   * backend/Services/WebhookNotifier.cs). Puede no venir si el aviso se
   * generó sin pasar por el backend real (ej. send-test-alert.js viejo). */
  correlationId?: string;
  /** Título corto, ej: "Incendio estructural" */
  title: string;
  /** Detalle del aviso, ej: dirección, tipo de emergencia */
  message: string;
  /** Dirección/ubicación del siniestro, si el backend la manda como texto */
  address?: string;
  /** Coordenadas del siniestro, si el backend las manda — permiten calcular
   * la distancia contra la ubicación actual del bombero (ver
   * AlertScreen/getDistanceToAlert). Ninguna de las dos sin la otra. */
  latitude?: number;
  longitude?: number;
  /** Timestamp ISO de cuándo se generó el aviso */
  createdAt: string;
}

export type AlertResponseType = 'ATTENDING' | 'NOT_ATTENDING';

export interface DeviceLocation {
  latitude: number;
  longitude: number;
  accuracy: number | null;
  timestamp: number;
}

export interface AlertResponsePayload {
  alertId: string;
  response: AlertResponseType;
  location: DeviceLocation | null;
  respondedAt: string;
}
