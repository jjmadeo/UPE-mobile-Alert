# Mobile Alert — App de alertas para bomberos

App React Native (Android primero, iOS a futuro) para avisar a bomberos de
una emergencia. Al llegar el aviso por push, la app muestra una pantalla
roja de pantalla completa —incluso con el teléfono bloqueado— con dos
botones: **Asistir** / **No asistir**. Cualquiera de los dos pide
confirmación antes de mandar la respuesta (para evitar toques accidentales)
y adjunta la ubicación actual del dispositivo.

## Stack y por qué

| Pieza | Elegido | Motivo |
|---|---|---|
| Push | `@react-native-firebase/messaging` (FCM) | Estándar de facto en RN, soporta Android e iOS. |
| Pantalla completa sobre lock screen | `@notifee/react-native` | Resuelve el patrón "full-screen intent" (como una llamada entrante) sin escribir una Activity nativa a mano. |
| Estado | `zustand` + `AsyncStorage` | Liviano, con persistencia de sesión e historial. |
| Navegación | `@react-navigation/native` (stack) | Solo Login/Home; la pantalla de alerta es un overlay, no una ruta (ver `App.tsx`). |
| Ubicación | `react-native-geolocation-service` | Más confiable que la API de geolocalización de RN core. |
| Backend | Mock en `/mock-server` (Express) | Todavía no hay backend real; implementa el mismo contrato que debería tener. |

React Native CLI puro (no Expo): el comportamiento de pantalla completa
sobre el lock screen necesita tocar `MainActivity` (ver
`android/.../MainActivity.kt`), algo que Expo managed workflow no permite sin
un dev client / prebuild.

## Setup

### 1. Instalar dependencias

```bash
npm install
```

### 2. Configurar Firebase (necesario para que el push funcione de verdad)

El repo trae `android/app/google-services.json` con valores **falsos** —
alcanza para que compile, pero ningún push real va a llegar hasta que lo
reemplaces:

1. Crear un proyecto en [Firebase Console](https://console.firebase.google.com).
2. Agregar una app Android con el package name `com.mobilealert.bomberos`
   (o el que hayas elegido — ver `android/app/build.gradle` → `applicationId`).
3. Descargar el `google-services.json` real y reemplazar
   `android/app/google-services.json`.
4. (Para el script de prueba de alertas) Ir a *Configuración del proyecto →
   Cuentas de servicio → Generar nueva clave privada* y guardar el JSON
   como `mock-server/serviceAccountKey.json` (está en `.gitignore`, no se
   commitea).

### 3. Levantar el mock backend

```bash
npm run mock-server
```

Queda escuchando en `http://localhost:4000`. Usuarios de prueba (definidos
en `mock-server/server.js`):

| Institución | Usuario | Password |
|---|---|---|
| BOMBEROS-CENTRAL | juan | 1234 |
| BOMBEROS-NORTE | maria | 1234 |

Si corrés la app en un dispositivo físico (no emulador), cambiá
`MOCK_BACKEND_URL` en `src/config/env.ts` por la IP de tu PC en la red
local — `10.0.2.2` solo funciona desde el emulador de Android Studio.

### 4. Correr la app

```bash
npm run android
```

Logueate con alguno de los usuarios de prueba. Al loguearte, la app pide
permiso de notificaciones y registra el token FCM contra el mock backend.

### 5. Probar una alerta de punta a punta

Con la app logueada al menos una vez (para que el token FCM haya quedado
registrado) y `serviceAccountKey.json` en su lugar:

```bash
npm run send-test-alert
```

Esto dispara un push real a través de Firebase al último dispositivo
registrado. Probalo en los tres escenarios que importan:

- **App abierta**: la pantalla roja aparece al instante.
- **App en background**: notificación con sonido tipo llamada; tocarla (o
  esperar el full-screen intent) abre la pantalla roja.
- **Teléfono bloqueado / app cerrada**: éste es el caso crítico. La pantalla
  debería prenderse sola y mostrar la alerta sobre el lock screen.

Parámetros opcionales: `npm run send-test-alert -- --title="..." --message="..." --address="..."`.

## Permisos y particularidades de Android que hay que conocer

- **Android 13+ (API 33)**: requiere `POST_NOTIFICATIONS`, se pide en
  runtime (`requestNotificationPermission` en `src/notifications/fcm.ts`).
- **Android 14+ (API 34) en adelante**: Google fue restringiendo cada vez
  más qué apps pueden usar `USE_FULL_SCREEN_INTENT` automáticamente. Es
  posible que en algunos dispositivos/versiones el usuario deba habilitarlo
  a mano la primera vez, en *Ajustes → Apps → Mobile Alert → Notificaciones
  a pantalla completa*. Conviene guiar a los bomberos con un instructivo de
  instalación que incluya este paso, y no asumir que el full-screen intent
  va a andar "solo" en el 100% de los equipos.
- **Optimización de batería / Doze**: fabricantes como Xiaomi, Huawei,
  Samsung, etc. matan procesos en background agresivamente. Para una app
  crítica como esta, hay que guiar al bombero a desactivar la optimización
  de batería para la app (`Ajustes → Batería → Sin restricciones`), o la
  alerta puede llegar tarde o no llegar.
- **Google Play**: si en el futuro se publica en Play Store, `USE_FULL_SCREEN_INTENT`
  está sujeto a políticas de uso aceptable (apps de alarma, llamadas,
  eventos de calendario). Como caso de bomberos/emergencias entra
  razonablemente en esa categoría, pero conviene revisar la política vigente
  al momento de publicar.

## Arquitectura de la app (carpetas clave)

```
src/
  types/            Tipos compartidos (AlertPayload, BrandingConfig, etc.)
  config/           URLs, ids de canal de notificación
  state/            zustand: authStore (sesión + branding), alertStore (aviso activo + historial)
  api/               axios contra el backend (login, registro de device token, respuesta a alerta)
  location/          Wrapper de geolocalización con manejo de permisos
  notifications/     FCM + Notifee: canal, full-screen intent, parseo de payload, eventos
  navigation/        Stack Login/Home (la alerta NO es una ruta, ver App.tsx)
  screens/           LoginScreen, HomeScreen, AlertScreen (la pantalla roja)
  components/        BigButton, ConfirmDialog
mock-server/         Backend mock (Express) + script para disparar un push de prueba
android/             App nativa (permisos, MainActivity con flags de lock screen, gradle de Firebase)
```

### Flujo de un aviso

1. El backend (hoy: `send-test-alert.js`) manda un mensaje FCM **solo con
   `data`**, nunca con `notification` (si llevara `notification`, Android
   mostraría su propia notificación default y nuestro código JS nunca se
   ejecutaría — ver comentario en `send-test-alert.js`).
2. `src/notifications/fcm.ts` recibe el mensaje (foreground u background) y
   llama a `displayAlertNotification`, que muestra una notificación Notifee
   de alta prioridad con `fullScreenAction`.
3. Si el teléfono está bloqueado, Android abre `MainActivity` directamente
   sobre el lock screen (gracias a los flags seteados en
   `MainActivity.kt`). Si el usuario toca la notificación, pasa lo mismo.
4. La app, al iniciar (`checkInitialNotification`) o al recibir el evento de
   Notifee (`subscribeToNotifeeForegroundEvents`), reconstruye el
   `AlertPayload` y lo pone en `alertStore.currentAlert`.
5. `App.tsx` dibuja `AlertScreen` como overlay apenas `currentAlert` no es
   `null`, sin importar en qué pantalla del stack estuviera el usuario.
6. El bombero toca Asistir/No asistir → `ConfirmDialog` pide confirmar →
   se obtiene la ubicación actual → se llama al backend
   (`respondToAlert`) → se cancela la notificación → se guarda en el
   historial local y desaparece la pantalla roja.

## White-label

No hay builds separados por institución. Al loguearse, la app manda un
`institutionCode` al backend, que devuelve un `BrandingConfig` (nombre,
color primario, logo, y su propio `backendUrl`). Ese branding se persiste
junto con la sesión (`authStore`) y se usa en `HomeScreen`. La pantalla de
alerta (`AlertScreen`) es **siempre roja**, a propósito: no debería
cambiar con el branding, es la señal de emergencia.

## Qué falta para producción (no es parte de este scaffold)

- **Backend real**: reemplazar `mock-server` por el backend definitivo,
  implementando el mismo contrato (`/api/auth/login`,
  `/api/devices/register`, `/api/alerts/:id/response`). El módulo de fan-out
  de alertas (mandar el push a todos los bomberos de una institución) no
  existe todavía — `send-test-alert.js` es solo para pruebas manuales.
- **Autenticación real**: el mock devuelve un token fake sin firmar. El
  backend real debería usar JWT firmado (o similar) con expiración.
- **iOS**: el código de FCM/ubicación ya es cross-platform, pero falta
  configurar el proyecto Xcode (APNs, certificados, `GoogleService-Info.plist`)
  y validar el comportamiento de pantalla completa en iOS, que tiene sus
  propias restricciones (no existe un equivalente directo al full-screen
  intent de Android; se puede explorar Live Activities / CallKit según el
  caso de uso).
- **Íconos/sonidos de marca**: hoy la notificación usa el ícono default de
  la app. Si cada institución necesita su propio sonido/ícono de alerta,
  hay que sumarlo al `BrandingConfig` y a `displayAlertNotification`.
- **Tests**: el scaffold incluye mocks de Jest para Firebase/Notifee/geolocation
  (`__mocks__/`) para que la suite corra, pero no hay tests más allá del
  smoke test que trae el template de React Native.
