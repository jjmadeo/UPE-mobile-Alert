import {
  getMessaging,
  getToken,
  onMessage,
  onTokenRefresh,
  setBackgroundMessageHandler,
  type RemoteMessage,
} from '@react-native-firebase/messaging';
import notifee, { EventType } from '@notifee/react-native';
import { displayAlertNotification } from './displayAlertNotification';
import { alertFromData } from './alertPayloadCodec';
import { useAlertStore } from '../state/alertStore';
import { useAuthStore } from '../state/authStore';
import { registerDeviceToken } from '../api/authApi';

const messaging = getMessaging();

/**
 * Pide permiso de notificaciones (POST_NOTIFICATIONS en Android 13+, prompt
 * nativo en iOS). Se delega todo en notifee: cubre ambas plataformas con una
 * sola API y evita depender de `messaging().requestPermission()`, que
 * react-native-firebase marcó como deprecada.
 */
export async function requestNotificationPermission(): Promise<boolean> {
  const settings = await notifee.requestPermission();
  // AuthorizationStatus: DENIED=0, AUTHORIZED=1, PROVISIONAL=2, EPHEMERAL=3
  return settings.authorizationStatus >= 1;
}

/** Registra (o re-registra) el token FCM actual contra el backend. No hace
 * nada si todavía no hay sesión iniciada. */
export async function syncFcmTokenWithBackend(): Promise<void> {
  if (!useAuthStore.getState().isAuthenticated) {
    return;
  }
  try {
    const token = await getToken(messaging);
    await registerDeviceToken(token);
  } catch (error) {
    console.warn('[fcm] no se pudo registrar el token en el backend', error);
  }
}

/**
 * Handler de mensajes en background/killed. IMPORTANTE: se debe registrar
 * en index.js, fuera de cualquier componente, antes de AppRegistry — ver
 * comentario en index.js.
 *
 * Muestra la notificación full-screen Y además escribe el aviso en el store
 * de Zustand. Esto último parece innecesario a primera vista ("no hay árbol
 * de React montado"), pero NO es cierto en el caso que más importa: cuando
 * el `fullScreenAction` de la notificación abre `MainActivity` sobre el
 * lock screen con el proceso recién arrancado en frío, este mismo handler y
 * el `App.tsx` que se monta a continuación corren en el mismo proceso/JS
 * engine — y confirmado a mano (ver historial): en ese cold start,
 * `notifee.getInitialNotification()` puede devolver `null` por una carrera
 * entre "Android ya lanzó la Activity vía el intent full-screen" y "notifee
 * terminó de registrar esa intent como la que originó el launch". El store
 * de Zustand no tiene ese problema: es un singleton en memoria que existe
 * apenas se importa el módulo, así que "escribirle antes de que exista
 * React" es simplemente el valor inicial que React va a leer en su primer
 * render, monte cuando monte. Si de verdad no hay proceso vivo para
 * mostrarlo (mensaje ignorado sin abrir la app), este set no tiene efecto
 * visible y no rompe nada.
 */
export function registerBackgroundMessageHandler(): void {
  setBackgroundMessageHandler(messaging, async remoteMessage => {
    const alert = alertFromData(remoteMessage.data);
    if (alert) {
      await displayAlertNotification(alert);
      useAlertStore.getState().showAlert(alert);
    }
  });
}

/** Mensajes recibidos con la app en foreground: mostramos la pantalla roja
 * directamente además de la notificación, para no depender de que el
 * bombero toque la notificación. */
export function subscribeToForegroundMessages(): () => void {
  return onMessage(messaging, async (remoteMessage: RemoteMessage) => {
    const alert = alertFromData(remoteMessage.data);
    if (!alert) {
      return;
    }
    await displayAlertNotification(alert);
    useAlertStore.getState().showAlert(alert);
  });
}

/** El token FCM puede rotar en cualquier momento; hay que re-registrarlo. */
export function subscribeToTokenRefresh(): () => void {
  return onTokenRefresh(messaging, async () => {
    await syncFcmTokenWithBackend();
  });
}

/** Notificación tocada (o abierta vía full-screen intent) con la app en
 * foreground o en background pero con el proceso vivo. */
export function subscribeToNotifeeForegroundEvents(): () => void {
  return notifee.onForegroundEvent(({ type, detail }) => {
    if (type === EventType.PRESS || type === EventType.DELIVERED) {
      const alert = alertFromData(detail.notification?.data);
      if (alert) {
        useAlertStore.getState().showAlert(alert);
      }
    }
  });
}

/** Notifee exige registrar un handler de background aunque no hagamos nada
 * con las acciones (no tenemos botones inline en la notificación); si no se
 * registra, notifee tira un warning en cada evento. */
export function registerNotifeeBackgroundHandler(): void {
  notifee.onBackgroundEvent(async () => {});
}

/**
 * Cold start: si la app se abrió porque el usuario tocó la notificación (o
 * el full-screen intent lanzó la Activity) con el proceso previamente
 * muerto, hay que recuperar el aviso desde acá — subscribeToNotifeeForegroundEvents
 * no llega a tiempo para este caso.
 */
export async function checkInitialNotification(): Promise<void> {
  const initial = await notifee.getInitialNotification();
  const alert = alertFromData(initial?.notification?.data);
  if (alert) {
    useAlertStore.getState().showAlert(alert);
  }
}
