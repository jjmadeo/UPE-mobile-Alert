# Mobile Alert API

Backend de la plataforma Mobile Alert. Implementado en C# sobre ASP.NET
Core Minimal API, con persistencia en PostgreSQL vía Entity Framework
Core.

> Para integrar el sistema de despacho de una institución con esta
> plataforma, ver [`INTEGRATION.md`](INTEGRATION.md). Este documento
> describe la ejecución y el funcionamiento interno del backend.

## Arquitectura

El backend es una plataforma intermediaria entre el sistema propio de cada
institución (cuartel) y la aplicación móvil de sus bomberos. No es dueño
de las credenciales de los bomberos: puede delegar la autenticación al
backend propio de la institución, o gestionarla localmente para
instituciones de prueba.

![Diagrama de arquitectura de Mobile Alert](../docs/diagrams/arquitectura.svg)

Dos esquemas de autenticación conviven en la API:

| Esquema | Utilizado por | Alcance |
|---|---|---|
| API key (`X-Api-Key`) | Backend del cuartel | Crear alertas, registrar webhooks |
| JWT (`Authorization: Bearer`) | Aplicación móvil | Registrar dispositivo, responder alertas |

## Casos de uso

Versión interactiva de todos los diagramas: [`docs/architecture.html`](../docs/architecture.html).

### Inicio de sesión

`POST /api/auth/login`. Según la institución tenga o no autenticación
delegada configurada, la contraseña se valida contra el backend propio del
cuartel o localmente (BCrypt), y se devuelve un token JWT junto con los
datos del bombero y la identidad de la institución.

![Diagrama de secuencia: inicio de sesión](../docs/diagrams/login.svg)

### Registro de dispositivo

`POST /api/devices/register`. Un token de dispositivo previamente asociado
a otro bombero es reasignado automáticamente, para cubrir el caso de un
mismo dispositivo físico reutilizado por otra cuenta.

![Diagrama de secuencia: registro de dispositivo](../docs/diagrams/dispositivo.svg)

### Creación y envío de una alerta

`POST /api/alerts`. El envío se considera exitoso de forma parcial: si
algunos destinatarios no son válidos o no tienen un dispositivo
registrado, la alerta igual se envía a los destinatarios restantes y el
detalle se informa en la respuesta. Un reenvío con el mismo identificador
de correlación se resuelve como una repetición idempotente. El envío se
reintenta automáticamente hasta la primera respuesta o el máximo de
reintentos configurado.

El envío a todos los dispositivos targeteados sale en paralelo, no uno
atrás del otro — importa para instituciones grandes, donde mandar
secuencial podría sumar varios segundos hasta que responda `POST
/api/alerts`. Por cada dispositivo se mandan además dos push (ver
`AlertService.FanOutAsync`): uno data-only, que dispara la pantalla
completa del lado de la app, y uno de respaldo con `notification` nativo,
que el sistema operativo del teléfono entrega igual aunque el fabricante
no deje correr el código de la app. Solo el primero cuenta para
`devicesNotified` en la respuesta.

![Diagrama de secuencia: creación y envío de una alerta](../docs/diagrams/alerta.svg)

### Respuesta a una alerta y notificación al cuartel

`POST /api/alerts/{id}/response`. La primera respuesta registrada detiene
los reintentos de envío para el resto de los destinatarios de esa alerta.
Si la institución tiene un webhook configurado, se notifica la respuesta
al backend del cuartel mediante una solicitud firmada.

![Diagrama de secuencia: respuesta a una alerta y notificación al cuartel](../docs/diagrams/respuesta.svg)

### Registro de webhook

`POST /api/webhooks`. La clave de firma se devuelve una única vez, en la
respuesta de este llamado.

![Diagrama de secuencia: registro de webhook](../docs/diagrams/webhook.svg)

## Ejecución local

Requiere Docker.

```bash
cd backend
docker compose up -d
```

En el primer arranque se aplican las migraciones y se cargan datos de
prueba. El servicio queda disponible en `http://localhost:5080`.

```bash
curl http://localhost:5080/api/health
```

El envío de push funciona sin configuración adicional: el repositorio
incluye `mock-server/serviceAccountKey.json`, la cuenta de servicio de un
proyecto de Firebase de prueba (plan Spark, sin facturación habilitada —
sin costo posible aunque se filtre). Si el archivo llegara a faltar, el
backend no rompe: `FcmSender` loguea un warning y las alertas se siguen
creando, solo que sin push real (ver `Services/FcmSender.cs`).

Para producción, generar una cuenta de servicio de un proyecto de Firebase
propio (Firebase Console → ⚙️ Configuración del proyecto → Cuentas de
servicio → "Generar nueva clave privada") y reemplazar el archivo. También
hace falta el `google-services.json` de ese mismo proyecto para compilar
el APK (`android/app/google-services.json`, no incluido en el repo — ver
`.gitignore`).

### Documentación interactiva de la API

`http://localhost:5080/scalar` — interfaz generada a partir del
documento OpenAPI (`/openapi/v1.json`), disponible en entorno de
desarrollo.

### Envío de alertas de prueba

```bash
./scripts/send-alert.sh     # Linux / macOS
./scripts/send-alert.ps1    # Windows
```

Ambos scripts simulan el llamado que realizaría el backend de un cuartel a
`POST /api/alerts`, utilizando la clave de API de prueba. Ver `--help`
(`.sh`) o `Get-Help ./scripts/send-alert.ps1` (`.ps1`) para las opciones
disponibles.

### Credenciales de prueba

| Institución | Usuario | Contraseña |
|---|---|---|
| BOMBEROS-CENTRAL | juan | 1234 |

Clave de API de la institución BOMBEROS-CENTRAL (uso: encabezado
`X-Api-Key`):

```
demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev
```

### Registro de una clave de API para producción

No existe todavía un endpoint de administración; el registro se realiza
directamente en la base de datos. Solo se almacena el hash de la clave:

```sql
INSERT INTO "ApiKeys" ("KeyHash", "Name", "InstitutionId", "IsActive", "CreatedAt")
VALUES ('<sha256-hex-de-la-clave>', 'Sistema de despacho — Institución X', <id-de-institucion>, true, now());
```

### Configuración de autenticación delegada

Tampoco existe un endpoint de administración para esto; se configura
directamente sobre la columna `LoginBackendUrl` de `Institutions` (`NULL`
por defecto, lo que deja la autenticación local activa):

```sql
UPDATE "Institutions" SET "LoginBackendUrl" = 'https://sistema-del-cuartel.example.com/login'
WHERE "Code" = 'BOMBEROS-CENTRAL';
```

Ver el contrato completo que debe cumplir esa URL en
[`INTEGRATION.md`](INTEGRATION.md#autenticación-delegada-opcional).

## Administración de la base de datos

```bash
cd backend
docker compose up -d pgadmin
```

Interfaz disponible en `http://localhost:5050`:

| | |
|---|---|
| Usuario | `dev@mobilealert.com` |
| Contraseña | `mobilealert` |
| Conexión | Preconfigurada como "mobilealert (docker-compose)"; solicita la contraseña de PostgreSQL (`mobilealert`) al conectar por primera vez |

## Modelo de datos

Entidades principales: `Institution`, `Firefighter`, `DeviceToken`,
`AlertRecord` y `AlertResponseRecord`.

| Campo | Propósito |
|---|---|
| `AlertRecord.TargetFirefighterIds` | Destinatarios efectivos de la alerta (subconjunto válido de los solicitados). Permite determinar quién no respondió, por diferencia con las respuestas registradas. |
| `AlertRecord.RequestPayload` / `ResponsePayload` | Copia del cuerpo de la solicitud y de la respuesta, para auditoría. |
| `AlertResponseRecord.WebhookUrl` / `WebhookRequestPayload` / `WebhookResponsePayload` / `WebhookStatusCode` | Auditoría de la entrega del webhook asociado a esa respuesta. |
| `CreatedAt` / `UpdatedAt` | Presentes en todas las entidades, gestionados automáticamente por el contexto de datos. |

## Endpoints

| Método | Ruta | Autenticación | Origen |
|---|---|---|---|
| POST | `/api/auth/login` | — | Aplicación móvil |
| POST | `/api/devices/register` | JWT | Aplicación móvil |
| POST | `/api/alerts` | API key | Backend del cuartel |
| POST | `/api/alerts/{id}/response` | JWT | Aplicación móvil |
| GET | `/api/alerts/{id}/responses` | — ⚠️ | Debug |
| POST | `/api/webhooks` | API key | Backend del cuartel |
| GET | `/api/health` | — | — |

⚠️ `GET /api/alerts/{id}/responses` no filtra por institución — cualquiera
con un `alertId` puede consultar sus respuestas, sin autenticación. Es
solo para debug manual (ver comentario en `AlertsEndpoints.cs`); no está
pensado como endpoint soportado, ni para el cuartel ni para la app.

## Tests

El proyecto `Tests/MobileAlert.Api.Tests` incluye dos suites:

- **Integración** (`CriticalFlowsTests`): ejercita la API completa por
  HTTP contra una instancia de PostgreSQL real (Testcontainers), cubriendo
  inicio de sesión, registro de dispositivo, creación y reenvío de
  alertas, y el ciclo completo de webhooks.
- **Unitarios** (`Unit/`): validan servicios individuales de forma
  aislada, incluyendo casos de aislamiento entre instituciones,
  idempotencia y manejo de errores del backend delegado.

```bash
cd backend
docker run --rm \
  --user "$(id -u):$(id -g)" --group-add "$(getent group docker | cut -d: -f3)" \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v "$(pwd)":/src \
  -v "$(pwd)/.dotnet-home":/home/dotnetuser \
  -e HOME=/home/dotnetuser \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test Tests/MobileAlert.Api.Tests
```

## Alcance actual

- No existe un endpoint de administración de claves de API ni webhooks;
  el registro se realiza directamente en la base de datos.
- Una clave de API válida puede operar sobre cualquier recurso de su
  propia institución, sin roles diferenciados.
- La imagen de contenedor utilizada en desarrollo (`dotnet watch`) no está
  pensada para producción; requiere una imagen de runtime independiente.
- La entrega de webhooks no se reintenta ante fallos; el resultado del
  intento queda registrado para auditoría.
- `GET /api/alerts/{id}/responses` es de debug: sin autenticación y sin
  aislamiento entre instituciones. No hay hoy un mecanismo soportado para
  que un cuartel recupere respuestas manualmente si falla la entrega del
  webhook.
