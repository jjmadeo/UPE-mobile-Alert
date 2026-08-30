# Mobile Alert

Sistema de alertas para bomberos compuesto por una aplicación móvil
(Android) y un backend propio. Al recibir un aviso, la aplicación muestra
una pantalla de alerta a pantalla completa, incluso con el dispositivo
bloqueado, con dos acciones disponibles: **Asistir** y **No asistir**.

## Arquitectura

Mobile Alert es una plataforma intermediaria entre el sistema de despacho
de cada institución (cuartel) y los dispositivos de sus bomberos. No
administra las credenciales de los bomberos por defecto; puede delegar la
autenticación al backend propio de la institución.

![Diagrama de arquitectura de Mobile Alert](docs/diagrams/arquitectura.svg)

Diagrama de arquitectura y de secuencia de cada caso de uso:
[Arquitectura y flujos](docs/architecture.html).

Documentación detallada del backend: [`backend/README.md`](backend/README.md).
Guía de integración para el backend de una institución:
[`backend/INTEGRATION.md`](backend/INTEGRATION.md).

## Instalación y prueba

Requisitos: Docker y un dispositivo Android en la misma red que el equipo
donde se ejecuta el backend.

### 1. Levantar el backend

```bash
git clone <url-del-repositorio>
cd mobile-alert/backend
docker compose up -d
```

El servicio queda disponible en `http://localhost:5080`.

```bash
curl http://localhost:5080/api/health
```

Documentación interactiva de la API: `http://localhost:5080/scalar`.

### 2. Instalar la aplicación

Instalar [`dist/mobile-alert.apk`](dist/mobile-alert.apk) en el
dispositivo Android (habilitar "Instalar aplicaciones de origen
desconocido" si el sistema lo requiere).

### 3. Configurar la dirección del backend

La aplicación necesita la dirección IP del equipo donde corre el backend en
la red local.

```bash
# Linux / macOS
ip addr show | grep "inet " | grep -v 127.0.0.1

# Windows (PowerShell)
ipconfig | Select-String "IPv4"
```

En la pantalla de inicio de sesión, seleccionar **Servidor** y completar
con `http://<IP-del-equipo>:5080`. El valor queda guardado en el
dispositivo.

### 4. Iniciar sesión

Credenciales de prueba, generadas automáticamente al levantar el backend:

| Institución | Usuario | Contraseña |
|---|---|---|
| BOMBEROS-CENTRAL | juan | 1234 |

Al iniciar sesión, la aplicación solicita permiso de notificaciones y
registra el dispositivo contra el backend.

### 5. Enviar una alerta de prueba

```bash
# Linux / macOS
./backend/scripts/send-alert.sh

# Windows
.\backend\scripts\send-alert.ps1
```

Sin argumentos adicionales, se envía una alerta de ejemplo al usuario
`juan`. Ver `--help` (`.sh`) o `Get-Help .\send-alert.ps1` (`.ps1`) para
las opciones disponibles (título, mensaje, ubicación, destinatarios).

### Administración de la base de datos

```bash
cd backend
docker compose up -d pgadmin
```

Interfaz disponible en `http://localhost:5050` (credenciales y detalle en
[`backend/README.md`](backend/README.md)).

## Estructura del repositorio

| Ruta | Contenido |
|---|---|
| `src/` | Aplicación móvil (React Native, TypeScript) |
| `android/` | Proyecto nativo Android |
| `dist/` | APK de release, lista para instalar |
| `backend/` | Backend (C# / .NET, PostgreSQL) |
| `backend/scripts/` | Utilitarios para disparar alertas de prueba |
| `backend/Tests/` | Tests de integración y unitarios |
| `docs/` | Diagramas de arquitectura y flujos (HTML + SVG) |
| `mock-server/` | Cuenta de servicio de Firebase de prueba, usada por el backend |

## Tecnologías

| Componente | Tecnología |
|---|---|
| Aplicación móvil | React Native (CLI), TypeScript |
| Notificaciones push | Firebase Cloud Messaging |
| Alertas a pantalla completa | Notifee (full-screen intent) |
| Estado de la aplicación | Zustand con persistencia local |
| Backend | ASP.NET Core Minimal API (.NET 10) |
| Base de datos | PostgreSQL con Entity Framework Core |

## Desarrollo

Para modificar el código de la aplicación (no solo instalar el APK
distribuido) hace falta además `android/app/google-services.json`, del
mismo proyecto de Firebase que la cuenta de servicio del backend (ver
"Ejecución local" en [`backend/README.md`](backend/README.md)) — se
descarga desde Firebase Console → ⚙️ Configuración del proyecto →
Configuración general → app Android. No está incluido en el repo (ver
`.gitignore`).

```bash
npm install
npm run android
```

Requiere un dispositivo o emulador Android conectado y accesible por
`adb`. La dirección del backend utilizada por defecto se define en
`src/config/env.ts` y puede modificarse desde la pantalla de inicio de
sesión sin recompilar.

### Generar el APK de release

```bash
cd android
./gradlew assembleRelease
```

El archivo resultante se genera en
`android/app/build/outputs/apk/release/app-release.apk`, firmado con el
keystore de desarrollo incluido en el repositorio. Para una publicación
formal (Google Play) se requiere un keystore propio.

## Consideraciones de la plataforma Android

- **Android 13+**: requiere el permiso `POST_NOTIFICATIONS`, solicitado en
  tiempo de ejecución.
- **Android 14+**: el uso de notificaciones a pantalla completa
  (`USE_FULL_SCREEN_INTENT`) puede requerir habilitación manual por parte
  del usuario en determinados dispositivos (Ajustes → Aplicaciones →
  Mobile Alert → Notificaciones a pantalla completa).
- **Gestión de energía**: algunos fabricantes (Xiaomi, Huawei, Samsung,
  entre otros) restringen procesos en segundo plano de forma agresiva, más
  allá de lo que deja ajustar Android estándar. La app ya usa las
  mitigaciones disponibles del lado del código — foreground service al
  mostrar la alerta y un push de respaldo en paralelo (ver "Entrega de la
  alerta" abajo) — pero para una entrega consistente conviene revisar
  además, en el teléfono:
  - Batería de la app en "Sin restricciones" (no "Optimizada").
  - En Samsung específicamente: Ajustes → Batería → Modo de ahorro de
    energía → desactivarlo (o agregar la app a las excepciones), y Batería
    adaptable/adaptativa → desactivarla (puede pisar el ajuste manual con
    el tiempo).
  - Modo de sonido del teléfono en "Sonido", no "Vibrar" — con el sistema
    en modo vibrar no suena ninguna notificación, sin importar la
    configuración de la app.
- **Entrega de la alerta**: cada dispositivo recibe dos push en paralelo —
  uno data-only, que dispara la pantalla completa del lado de la app, y
  uno de respaldo con notificación nativa de Android, que el sistema
  entrega igual aunque el fabricante no deje correr el código de la app.
  En el peor caso, el respaldo garantiza sonido y una notificación
  visible aunque no se logre la pantalla completa automática (ver
  `backend/Services/FcmSender.cs` y `src/notifications/displayAlertNotification.ts`).
- **Tráfico HTTP**: la aplicación permite tráfico sin cifrar
  (`android/app/src/main/res/xml/network_security_config.xml`) para
  soportar backends autoalojados sin certificado TLS. En un despliegue con
  dominio y certificado propios, esta configuración debe removerse.

## Estado del proyecto

- **iOS**: no implementado. El código de notificaciones y ubicación es
  multiplataforma; resta la configuración del proyecto Xcode y la
  validación del comportamiento de pantalla completa en iOS.
- **Personalización por institución**: la aplicación soporta un color e
  identidad por institución (`BrandingConfig`); no incluye sonidos o
  íconos de notificación personalizados.
- Ver también la sección "Alcance actual" en
  [`backend/README.md`](backend/README.md).
