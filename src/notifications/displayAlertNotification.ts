import notifee, {
  AndroidCategory,
  AndroidForegroundServiceType,
  AndroidImportance,
} from '@notifee/react-native';
import { ensureAlertChannel } from './channel';
import { alertToNotifeeData } from './alertPayloadCodec';
import { AlertPayload } from '../types/alert';
import { colors } from '../theme';

/**
 * Muestra la alerta como notificación full-screen tipo "llamada entrante":
 * suena, vibra y si el teléfono está bloqueado abre la app directamente
 * sobre el lock screen (fullScreenAction). Se usa tanto desde el handler de
 * foreground como el de background/killed — el comportamiento tiene que ser
 * idéntico en los tres estados.
 *
 * `asForegroundService: true` (Android): además de mostrarla, arranca un
 * foreground service — mantiene el proceso vivo con prioridad elevada
 * durante los primeros minutos, que es la ventana en la que algunos
 * fabricantes (confirmado con Samsung) matan el proceso antes de que el
 * fullScreenAction llegue a dispararse, aunque la app tenga la batería sin
 * restricciones. El tipo lo fuerza notifee-core a `shortService`
 * (foregroundServiceTypes acá solo tiene que coincidir con eso), así que
 * Android lo corta solo pasado un rato — por eso igual hace falta
 * `cancelAlertNotification` para pararlo apenas el bombero responde.
 */
export async function displayAlertNotification(
  alert: AlertPayload,
): Promise<void> {
  const channelId = await ensureAlertChannel();

  // Cancelar antes de mostrar, siempre, aunque no haya ninguna con ese id
  // todavía (no-op inofensivo en ese caso). Es la única forma de que el
  // full-screen intent vuelva a dispararse en un reenvío: para Android,
  // `notify()` con un id que YA tiene una notificación activa es una
  // actualización (suena/vibra de nuevo, pero NO relanza el full-screen
  // intent aunque el teléfono siga bloqueado) — y el backend reintenta con
  // el mismo alertId a propósito (ver mock-server/send-test-alert.js) para
  // que sea la MISMA notificación, no una nueva apilada. Cancelar primero
  // hace que el siguiente `displayNotification` se vea como recién posteada
  // y dispare el full-screen intent de nuevo.
  await notifee.cancelNotification(alert.id);

  await notifee.displayNotification({
    id: alert.id,
    title: `🚨 ${alert.title}`,
    body: alert.address ? `${alert.message}\n${alert.address}` : alert.message,
    data: alertToNotifeeData(alert),
    android: {
      channelId,
      category: AndroidCategory.CALL,
      importance: AndroidImportance.HIGH,
      color: colors.red600,
      autoCancel: false,
      ongoing: true,
      sound: 'default',
      loopSound: true,
      showTimestamp: true,
      asForegroundService: true,
      foregroundServiceTypes: [AndroidForegroundServiceType.FOREGROUND_SERVICE_TYPE_SHORT_SERVICE],
      pressAction: { id: 'default', launchActivity: 'default' },
      fullScreenAction: { id: 'default', launchActivity: 'default' },
    },
  });
}

/** Se llama una vez que el bombero respondió, para sacar la notificación
 * "en curso" de la bandeja (dejó de estar ongoing/loop) y frenar el
 * foreground service que arrancó displayAlertNotification — si no se
 * detiene acá, igual lo corta Android solo al rato (shortService), pero no
 * tiene sentido dejarlo corriendo de más una vez que ya se respondió. */
export async function cancelAlertNotification(alertId: string): Promise<void> {
  await notifee.cancelNotification(alertId);
  await notifee.stopForegroundService();
}
