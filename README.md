# Mobile Alert — App de alertas para bomberos

App React Native (Android) + backend real en C#/.NET para avisar a bomberos
de una emergencia. Al llegar el aviso por push, la app muestra una pantalla
roja de pantalla completa —incluso con el teléfono bloqueado— con dos
botones: **Asistir** / **No asistir**. Cualquiera de los dos pide
confirmación antes de mandar la respuesta (para evitar toques accidentales)
y adjunta la ubicación actual del dispositivo.

```
Backend del cuartel (propio)  ──── API key ────▶  Mobile Alert API  ──── FCM ────▶  App mobile
        ▲                                                 │
        └──────────────── webhook (respuestas) ───────────┘
```

Esta plataforma **no es dueña de los bomberos de nadie** — es un
intermediario entre el sistema propio de cada cuartel y la app mobile. Ver
[`backend/INTEGRATION.md`](backend/INTEGRATION.md) si estás integrando el
backend de TU cuartel.

## Probarlo en 5 minutos

Todo corre en Docker — no hace falta instalar .NET, Postgres ni Android
Studio. Lo único que necesitás en la PC es Docker, y un teléfono Android en
la **misma red WiFi**.

### 1. Levantar el backend

```bash
git clone <este-repo>
cd mobile-alert/backend
docker compose up -d
```

Primer arranque: baja las imágenes, corre las migraciones y siembra datos
de prueba — tarda un par de minutos. Después queda escuchando en
`http://localhost:5080` (`curl http://localhost:5080/api/health` para
confirmar). Documentación interactiva de la API en
`http://localhost:5080/scalar`.

### 2. Instalar la app en el teléfono

Bajate [`dist/mobile-alert.apk`](dist/mobile-alert.apk) e instalalo (activar
"Instalar apps de fuentes desconocidas" si Android lo pide la primera vez).
Es un build de release autocontenido — no necesita Metro ni ninguna otra
cosa corriendo, solo poder llegar al backend por red.

### 3. Decirle a la app dónde está tu backend

El teléfono necesita la IP de tu PC en la red local (no `localhost`, eso
sería el teléfono mismo). Para encontrarla:

```bash
# Linux / Mac
ip addr show | grep "inet " | grep -v 127.0.0.1

# Windows (PowerShell)
ipconfig | Select-String "IPv4"
```

Abrí la app → en el login, tocá **"Servidor: ..."** (arriba del formulario)
→ reemplazá por `http://TU-IP:5080` (ej: `http://192.168.0.28:5080`) → esa
URL queda guardada en el teléfono, no hace falta tocarla de nuevo salvo que
cambie la IP de tu PC.

> Si el teléfono no puede conectarse: confirmá que está en la misma red que
> la PC, y que el firewall del host no esté bloqueando el puerto 5080
> (`sudo ufw allow 5080/tcp` en Linux con ufw activo).

### 4. Loguearte

Usuarios de prueba (institución + usuario + contraseña), sembrados
automáticamente:

| Institución | Usuario | Contraseña |
|---|---|---|
| BOMBEROS-CENTRAL | juan | 1234 |
| BOMBEROS-NORTE | maria | 1234 |

Al loguearte, la app pide permiso de notificaciones y registra el token FCM
del teléfono contra el backend — **necesario antes del paso 5.**

### 5. Mandar una alerta de prueba

Desde la PC (no hace falta Node — son scripts nativos):

```bash
# Linux / Mac / WSL
./backend/scripts/send-alert.sh

# Windows
.\backend\scripts\send-alert.ps1
```

Sin argumentos, manda una alerta genérica al usuario `juan` de
`BOMBEROS-CENTRAL`. Con la app logueada como `juan`, en unos segundos
debería sonar como si fuera una llamada entrante y mostrar la pantalla roja
— probalo con la app abierta, en background, y con el teléfono bloqueado
(el caso crítico de verdad).

Parámetros disponibles (`--help` en el `.sh`, `Get-Help .\send-alert.ps1`
en el `.ps1`): título, mensaje, dirección, latitud/longitud, a qué
`firefighterIds` mandarla, y contra qué URL de backend (`--backend-url` /
`-BackendUrl`, por si no es `localhost:5080`).

## Herramientas opcionales para mirar la base

```bash
cd backend
docker compose run --rm harlequin   # IDE de SQL por terminal
docker compose up -d pgadmin        # UI web en http://localhost:5050
```

Ver [`backend/README.md`](backend/README.md) para credenciales y detalle de
cada una.

## Qué hay en el repo

```
src/                App React Native (TypeScript)
android/             Proyecto nativo Android
dist/                APK de release ya compilado, listo para instalar
backend/             Backend real — C#/.NET 10 Minimal APIs + Postgres
  scripts/           send-alert.sh / .ps1 / .js — disparar alertas de prueba
  Tests/             Tests de integración (Testcontainers) y unitarios
  tools/             Harlequin y pgAdmin, containerizados
  INTEGRATION.md     Guía para integrar el backend de TU cuartel
  README.md          Arquitectura, endpoints, modelo de datos, cómo correr los tests
mock-server/         Mock viejo (Express) — reemplazado por backend/, se
                     mantiene solo como referencia de contrato mínimo
```

## Stack y por qué

| Pieza | Elegido | Motivo |
|---|---|---|
| App: Push | `@react-native-firebase/messaging` (FCM) | Estándar de facto en RN, soporta Android e iOS. |
| App: Pantalla completa sobre lock screen | `@notifee/react-native` | Resuelve el patrón "full-screen intent" (como una llamada entrante) sin escribir una Activity nativa a mano. |
| App: Estado | `zustand` + `AsyncStorage` | Liviano, con persistencia de sesión, historial, y URL del servidor configurable. |
| App: Navegación | `@react-navigation/native` (stack) | Solo Login/Home; la pantalla de alerta es un overlay, no una ruta (ver `App.tsx`). |
| App: Ubicación | `react-native-geolocation-service` | Más confiable que la API de geolocalización de RN core. |
| Backend: API | C# / ASP.NET Core Minimal APIs | Tipado, orientado a objetos, sin el peso de MVC completo para una API chica. |
| Backend: datos | PostgreSQL + EF Core | `jsonb` para auditoría de payloads crudos, arrays nativos para targets de alerta. |

React Native CLI puro (no Expo): el comportamiento de pantalla completa
sobre el lock screen necesita tocar `MainActivity` (ver
`android/.../MainActivity.kt`), algo que Expo managed workflow no permite sin
un dev client / prebuild.

## Desarrollo (correr la app desde código, no el APK ya compilado)

Para tocar código de la app (no solo instalar el APK de `dist/`), hace
falta el SDK de Android + Metro corriendo. Ver
[`backend/README.md`](backend/README.md) para levantar el backend en modo
dev (`dotnet watch`, hot reload).

```bash
npm install
npm run android   # necesita un emulador o dispositivo con adb ya conectado
```

`src/config/env.ts` tiene el valor semilla de `serverConfigStore` (la URL
del backend configurable desde el login, ver paso 3 de arriba) — cambialo
si tu setup de desarrollo lo necesita por default en otro valor.

### Compilar el APK de release vos mismo

```bash
cd android
./gradlew assembleRelease
# resultado en android/app/build/outputs/apk/release/app-release.apk
```

Usa el mismo keystore de debug que trae el repo (`android/app/debug.keystore`)
— suficiente para instalar/probar, no para publicar en Google Play (ver
comentario en `android/app/build.gradle`).

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
  config/           Valor semilla de URL de backend, ids de canal de notificación
  state/            zustand: authStore (sesión + branding), alertStore (aviso activo + historial), serverConfigStore (URL del backend, editable)
  api/               axios contra el backend (login, registro de device token, respuesta a alerta)
  location/          Wrapper de geolocalización con manejo de permisos
  notifications/     FCM + Notifee: canal, full-screen intent, parseo de payload, eventos
  navigation/        Stack Login/Home (la alerta NO es una ruta, ver App.tsx)
  screens/           LoginScreen, HomeScreen, AlertScreen (la pantalla roja)
  components/        BigButton, ConfirmDialog
backend/             Backend real — ver backend/README.md
android/             App nativa (permisos, MainActivity con flags de lock screen, gradle de Firebase)
```

### Flujo de un aviso

1. El backend del cuartel manda `POST /api/alerts` (ver
   [`backend/INTEGRATION.md`](backend/INTEGRATION.md)); nuestro backend
   manda un mensaje FCM **solo con `data`**, nunca con `notification` (si
   llevara `notification`, Android mostraría su propia notificación default
   y nuestro código JS nunca se ejecutaría).
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
   historial local, desaparece la pantalla roja, y el backend le avisa al
   cuartel por webhook (ver `backend/INTEGRATION.md`).

## White-label

No hay builds separados por institución. Al loguearse, la app manda un
`institutionCode` al backend, que devuelve un `BrandingConfig` (nombre,
color primario, logo, y su propio `backendUrl`). Ese branding se persiste
junto con la sesión (`authStore`) y se usa en `HomeScreen`. La pantalla de
alerta (`AlertScreen`) es **siempre roja**, a propósito: no debería
cambiar con el branding, es la señal de emergencia.

## Qué falta para producción

- **HTTPS**: la app permite tráfico HTTP plano a propósito
  (`android/app/src/main/res/xml/network_security_config.xml`), porque hoy
  el backend corre self-hosted en una LAN casera sin certificado. El día
  que el backend tenga un dominio propio con TLS de verdad, ese archivo
  debería borrarse (Android bloquea cleartext por default en release desde
  hace rato, y así debería quedar).
- **iOS**: el código de FCM/ubicación ya es cross-platform, pero falta
  configurar el proyecto Xcode (APNs, certificados, `GoogleService-Info.plist`)
  y validar el comportamiento de pantalla completa en iOS, que tiene sus
  propias restricciones (no existe un equivalente directo al full-screen
  intent de Android; se puede explorar Live Activities / CallKit según el
  caso de uso).
- **Íconos/sonidos de marca**: hoy la notificación usa el ícono default de
  la app. Si cada institución necesita su propio sonido/ícono de alerta,
  hay que sumarlo al `BrandingConfig` y a `displayAlertNotification`.
- **Firma de release de verdad**: el APK de `dist/` está firmado con el
  keystore de debug (suficiente para instalar/probar) — para Google Play
  hace falta generar un keystore propio, ver
  [la doc oficial de RN](https://reactnative.dev/docs/signed-apk-android).
- Ver también la sección "Qué falta (a propósito, no es un olvido)" en
  [`backend/README.md`](backend/README.md) para lo que falta del lado del
  backend (endpoint de administración de API keys, reintento de webhooks
  fallidos, etc.).
