import { Platform, PermissionsAndroid } from 'react-native';
import Geolocation from 'react-native-geolocation-service';
import { DeviceLocation } from '../types/alert';

async function requestPermission(): Promise<boolean> {
  if (Platform.OS === 'android') {
    const granted = await PermissionsAndroid.request(
      PermissionsAndroid.PERMISSIONS.ACCESS_FINE_LOCATION,
      {
        title: 'Permiso de ubicación',
        message:
          'Para avisar desde dónde vas a responder, la app necesita acceso a tu ubicación.',
        buttonPositive: 'Permitir',
        buttonNegative: 'No permitir',
      },
    );
    return granted === PermissionsAndroid.RESULTS.GRANTED;
  }
  // iOS: react-native-geolocation-service maneja el prompt del sistema
  // internamente al llamar getCurrentPosition, pero requestAuthorization
  // explícito da mejor control del resultado.
  const authStatus = await Geolocation.requestAuthorization('whenInUse');
  return authStatus === 'granted';
}

const NATIVE_TIMEOUT_MS = 10000;
// Colchón de seguridad por arriba del timeout que le pasamos al nativo: en
// algunos dispositivos/emuladores (confirmado acá con el emulador Docker;
// probablemente un FusedLocationProviderClient sin fix de GPS real)
// `Geolocation.getCurrentPosition` no respeta su propio `timeout` y el
// callback de error nunca llega. Como esto bloquea el flujo de
// Asistir/No asistir — justo el momento en que el bombero menos puede
// esperar — hay que ganarle la carrera con un timeout propio en JS, sin
// depender de que la librería nativa cumpla el suyo.
const SAFETY_TIMEOUT_MS = NATIVE_TIMEOUT_MS + 3000;

function timeoutAfter(ms: number): Promise<null> {
  return new Promise(resolve => setTimeout(() => resolve(null), ms));
}

/**
 * Devuelve la ubicación actual del dispositivo o `null` si el usuario no dio
 * permiso, falló la obtención (ej: GPS apagado), o se venció el timeout de
 * seguridad. Nunca lanza: la respuesta de "asistir/no asistir" tiene que
 * poder enviarse igual aunque no haya ubicación disponible, para no
 * bloquear al bombero en un momento crítico.
 */
export async function getCurrentLocation(): Promise<DeviceLocation | null> {
  try {
    const hasPermission = await requestPermission();
    if (!hasPermission) {
      return null;
    }

    const locationPromise = new Promise<DeviceLocation | null>(resolve => {
      Geolocation.getCurrentPosition(
        position => {
          resolve({
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracy: position.coords.accuracy ?? null,
            timestamp: position.timestamp,
          });
        },
        error => {
          console.warn('[getCurrentLocation] error', error);
          resolve(null);
        },
        { enableHighAccuracy: true, timeout: NATIVE_TIMEOUT_MS, maximumAge: 0 },
      );
    });

    return await Promise.race([
      locationPromise,
      timeoutAfter(SAFETY_TIMEOUT_MS),
    ]);
  } catch (error) {
    console.warn('[getCurrentLocation] unexpected error', error);
    return null;
  }
}
