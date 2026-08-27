# Mobile Alert API — backend real

Backend real (reemplaza a `mock-server/` de a poco) para la app de alertas de
bomberos. C# / ASP.NET Core Minimal APIs, tipado y orientado a objetos,
persistencia en PostgreSQL vía EF Core.

## Arquitectura: plataforma multi-cuartel, no dueño de los usuarios

Este backend **no es** el sistema de un cuartel — es una plataforma
intermediaria entre el backend propio de cada cuartel y la app mobile:

```
Backend del cuartel (propio)  ──── API key ────▶  Mobile Alert API  ──── FCM ────▶  App mobile
        ▲                                                 │
        └──────────────── webhook (respuestas) ───────────┘
```

- **`POST /api/alerts`** — el backend del cuartel dispara una alerta acá
  (autenticado con su API key, header `X-Api-Key`), pasando un array de
  `firefighterIds` **explícito** (nuestros ids internos, los mismos que
  devuelve el login) — no manda a "toda la institución" automáticamente.
  Siempre `200`: se prioriza mandar lo que se pueda mandar, aunque sea a
  cero destinatarios; el detalle de qué id no existe o no tiene device
  (`unknownFirefighterIds` / `firefightersWithoutDevice`) va en el body,
  nunca en el status code — ver `Services/AlertService.cs`. Reintenta cada
  10s (configurable) hasta que alguien responda —
  `Services/AlertRetryBackgroundService.cs`.
- **`POST /api/webhooks`** — el cuartel registra una URL propia (misma auth
  por API key). Cada vez que un bombero responde, le pegamos ahí —
  `Services/WebhookNotifier.cs`, firmado con HMAC-SHA256 (header
  `X-Signature`, hex del HMAC del body con el `secret` que devuelve este
  mismo endpoint, una sola vez, al crear la suscripción).
- **Login delegado** — `POST /api/auth/login` reenvía usuario/contraseña al
  `LoginBackendUrl` propio de la institución (si lo tiene configurado) y
  espera `200 { name, externalId? }` para considerarlo válido. Si la
  institución no tiene `LoginBackendUrl` (como las dos de prueba, sembradas
  por `Data/DbSeeder.cs`), se valida localmente contra
  `Firefighter.PasswordHash` (BCrypt) — ver `Services/AuthService.cs`.
- El **JWT que devuelve el login** (`Services/JwtTokenService.cs`) es la
  credencial que usa la APP MOBILE contra el resto de los endpoints
  (`/api/devices/register`, `/api/alerts/{id}/response`) — es un scheme de
  auth totalmente separado de la API key de los cuarteles.

## Correr en local

Todo en Docker, nada instalado en la máquina (ni el SDK de .NET ni Postgres):

```bash
cd backend
docker compose up -d
```

Primer arranque: baja la imagen del SDK, restaura paquetes, corre las
migraciones de EF Core y siembra datos de prueba — tarda un poco más que los
siguientes (`dotnet watch` después queda recompilando solo al guardar un
archivo).

Queda escuchando en **`http://localhost:5080`**. Health check:

```bash
curl http://localhost:5080/api/health
```

### ⚠️ Si cambiás algo que solo corre una vez al arrancar

`dotnet watch` con hot-reload **no** vuelve a ejecutar configuración de
`Program.cs` que solo corre una vez al boot (registro de servicios, opciones
de JWT, etc.) — si tocás algo ahí y no ves el efecto, `docker compose
restart api` en vez de esperar al hot-reload.

### Usuarios de prueba (instituciones sin `LoginBackendUrl`, auth local)

| Institución | Usuario | Contraseña |
|---|---|---|
| BOMBEROS-CENTRAL | juan | 1234 |
| BOMBEROS-NORTE | maria | 1234 |

### API key de prueba

Se loguea una vez, en el primer arranque (`docker compose logs api \| grep
"API key de prueba"`) — es fija a propósito para que sea la misma en cada
`docker compose up` limpio:

```
demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev
```

Institución: BOMBEROS-CENTRAL. Uso: header `X-Api-Key`.

### Cargar una API key real a mano

No hay endpoint de administración todavía (a propósito, ver conversación de
diseño) — se inserta directo en Postgres. El valor real de la key nunca se
guarda, solo su hash:

```sql
-- Elegí un valor random largo vos mismo (ej: openssl rand -hex 32) y
-- calculá su SHA-256 en hex ANTES de este INSERT.
INSERT INTO "ApiKeys" ("KeyHash", "Name", "InstitutionId", "IsActive", "CreatedAt")
VALUES ('<sha256-hex-de-la-key>', 'Sistema de despacho — Bomberos X', <institution-id>, true, now());
```

Con `psql` (adentro del contenedor `db`, `docker compose exec db psql -U
mobilealert`) podés generar el hash ahí mismo:

```sql
SELECT encode(sha256('tu-key-random-acá'::bytea), 'hex');
```

## Tests

`Tests/MobileAlert.Api.Tests` tiene dos capas, en dos carpetas separadas
según qué necesitan para correr:

### Integración (`Tests/MobileAlert.Api.Tests/CriticalFlowsTests.cs`)

De punta a punta de verdad: cada test le pega por HTTP a la API completa
hosteada en memoria (`ApiFactory`, con `WebApplicationFactory<Program>`),
corriendo Program.cs tal cual (auth, migraciones, seed, todos los
endpoints), contra una Postgres real levantada por Testcontainers — no el
proveedor InMemory de EF Core, que no entiende `jsonb` ni los `int[]`
nativos que usan `AlertRecord`/`AlertResponseRecord`. Lo único sustituido es
`IFcmSender` (por `FakeFcmSender`, que registra qué se le mandó) para no
depender de credenciales reales de Firebase.

- **Login + registro de device**: se registra un token, y un segundo
  registro del mismo bombero pisa al anterior.
- **Alerta creada → fan-out → FCM** efectivamente invocado, con los datos
  correctos; replay idempotente por `correlationId`.
- **Webhooks**: alta de una suscripción (persiste, devuelve el secret una
  sola vez, rechaza URLs inválidas) y, de punta a punta, respuesta del
  bombero → webhook entregado, firmado con ESE secret, y auditado en la base
  (`WebhookUrl`/`WebhookStatusCode`/`WebhookRequestPayload`).

Necesitan el socket de Docker montado (Testcontainers levanta la Postgres de
prueba llamando al daemon del host, "Docker fuera de Docker").

### Unitarios (`Tests/MobileAlert.Api.Tests/Unit/`)

Cada servicio de negocio por separado, sin HTTP — contra una Postgres en
memoria (`Microsoft.EntityFrameworkCore.InMemory`, un nombre de base random
por test) en vez de una real, o directamente sin ninguna DB donde no hace
falta. Rápidos (todos juntos corren en menos de un segundo) y cubren casos
puntuales que serían tediosos de armar por HTTP:

- **`ApiKeyAuthTests`** — el hash de una API key es determinístico y nunca
  expone la key cruda.
- **`JwtTokenServiceTests`** — el JWT lleva los claims correctos, valida con
  el secreto configurado y falla con cualquier otro, y expira cuando
  corresponde. (Al escribir el test que valida el claim `sub` se pisó el
  MISMO bug de `MapInboundClaims` que ya había aparecido una vez en
  Program.cs — quedó comentado ahí.)
- **`DeviceServiceTests`** — un token nuevo pisa al viejo del mismo bombero;
  un token que "pertenecía" a otro bombero (mismo teléfono, otra cuenta) se
  reasigna, no queda duplicado en dos dueños.
- **`AlertServiceTests`** — un id de OTRA institución se reporta como
  "unknown" (no se filtra que existe en algún lado — aislamiento
  multi-tenant); replay idempotente no vuelve a mandar el push; una segunda
  respuesta del mismo bombero actualiza en vez de duplicar fila.
- **`AuthServiceTests`** — login local (BCrypt) y delegado (reenvía
  usuario/contraseña tal cual al backend del cuartel, sincroniza el nombre
  en cada login, no guarda `PasswordHash` para bomberos delegados) por
  separado, incluyendo que un backend de cuartel caído o que devuelve un
  error nunca se cuela como una excepción sin capturar — siempre
  `InvalidLoginException`.

No necesitan Docker socket ni Testcontainers — corren en el mismo
`dotnet test` que los de integración (un solo comando corre las dos
capas):

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

El socket de Docker igual hace falta en el comando porque los de
integración SÍ lo usan (Testcontainers) — si algún día se separan en
proyectos de test distintos, los unitarios podrían correr sin montarlo.
`--group-add` con el gid del grupo `docker` del host es necesario para que
el usuario sin privilegios (1000:1000, mismo motivo que el servicio `api` de
docker-compose.yml) pueda hablar con el socket.

## Mirar/tocar la base a mano (Harlequin)

[Harlequin](https://harlequin.sh) es un IDE de SQL por terminal (TUI). Corre
en Docker (`tools/harlequin/Dockerfile`), conectado a la misma Postgres por
la red interna de compose — nada instalado en el host:

```bash
cd backend
docker compose run --rm harlequin
```

`run`, no `up` — es un cliente interactivo que se abre y se cierra, no un
servicio que tenga que quedar corriendo. La primera vez buildea la imagen
sola si no existe (o `docker compose build harlequin` a mano). Adentro:
tab para moverse entre el árbol de tablas/schema y el editor, `F5` o
`ctrl+enter` para correr la query.

## Modelo de datos

`Institution` → `Firefighter` → `DeviceToken`, más `AlertRecord` ↔
`AlertResponseRecord` para el ciclo de una alerta. Puntos que no son obvios
mirando solo el código:

- **`AlertRecord.TargetFirefighterIds`** (`int[]`, array nativo de
  Postgres) es a quién se le mandó de verdad — el subconjunto válido de
  `firefighterIds` que vino en el request. No hay una tabla aparte con una
  fila por bombero por alerta (existió, se sacó a propósito): un array es
  suficiente para lo que hace falta (fan-out y reintento leen de ahí
  directo) y evita filas de más. "¿Quién no respondió todavía?" =
  `TargetFirefighterIds` menos los `FirefighterId` que aparecen en
  `AlertResponseRecord` para esa alerta — ver
  `AlertService.GetUnansweredFirefighterIdsAsync`.
- **`AlertRecord.RequestPayload` / `ResponsePayload`** (`jsonb`) — el body
  crudo que llegó a `POST /api/alerts` y el que devolvimos, tal cual, sin
  recortar. Auditoría/debug: "¿qué mandó exactamente el cuartel tal día?"
  sin tener que reconstruirlo de las columnas tipadas.
- **`AlertResponseRecord.Webhook*`** (`Url`, `RequestPayload`,
  `ResponsePayload`, `StatusCode`) — auditoría de la entrega del webhook
  para ESA respuesta puntual. Asume un solo webhook activo por institución
  (si hay más de uno, solo queda el resultado del último intentado).
- **`IAuditable`** (`CreatedAt`/`UpdatedAt`) en las 8 entidades, completado
  solo por `AppDbContext.SaveChanges` — nunca a mano en un servicio.

## Endpoints

| Método | Ruta | Auth | Quién lo llama |
|---|---|---|---|
| POST | `/api/auth/login` | — | App mobile |
| POST | `/api/devices/register` | JWT | App mobile |
| POST | `/api/alerts` | API key | Backend del cuartel |
| POST | `/api/alerts/{id}/response` | JWT | App mobile |
| GET | `/api/alerts/{id}/responses` | — (debug) | Debug manual |
| POST | `/api/webhooks` | API key | Backend del cuartel |
| GET | `/api/health` | — | — |

## Qué falta (a propósito, no es un olvido)

- **Endpoint de administración de API keys/webhooks** — hoy se cargan a
  mano en la base. Ver decisión en la conversación de diseño: se priorizó
  dejar andando el flujo de alertas primero.
- **Rol de "despachador"** para `POST /api/alerts` — hoy cualquier API key
  válida puede mandar alertas a SU institución (no a otras), pero no hay
  granularidad más fina que esa.
- **Dockerfile de producción** — hoy `docker-compose.yml` corre la imagen
  completa del SDK con `dotnet watch` (para hot-reload en desarrollo). Un
  build real necesita un Dockerfile multi-stage con la imagen runtime, no
  el SDK completo.
- **Reintento de webhooks fallidos** — si el webhook del cuartel está caído
  en el momento de la respuesta, hoy se loguea y no se reintenta.
