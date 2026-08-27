import notifee, {
  AndroidImportance,
  AndroidVisibility,
} from '@notifee/react-native';
import {
  NOTIFICATION_CHANNEL_ID,
  NOTIFICATION_CHANNEL_NAME,
} from '../config/env';
import { colors } from '../theme';

/**
 * Canal de Android con IMPORTANCE_HIGH: es un requisito de la plataforma
 * para que el `fullScreenAction` de una notificación pueda efectivamente
 * despertar la pantalla y mostrarse sobre el lock screen (heads-up +
 * full-screen intent). Con importancia menor, Android la trata como
 * notificación silenciosa y el full screen intent no dispara.
 */
export async function ensureAlertChannel(): Promise<string> {
  return notifee.createChannel({
    id: NOTIFICATION_CHANNEL_ID,
    name: NOTIFICATION_CHANNEL_NAME,
    importance: AndroidImportance.HIGH,
    visibility: AndroidVisibility.PUBLIC,
    sound: 'default',
    vibration: true,
    vibrationPattern: [300, 500, 300, 500],
    lights: true,
    lightColor: colors.red600,
  });
}
