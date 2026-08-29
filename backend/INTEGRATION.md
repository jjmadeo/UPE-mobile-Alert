# Guía de integración — backend de tu cuartel

Esta guía es para el equipo de desarrollo de un cuartel que quiere que **su
propio sistema** dispare alertas a través de Mobile Alert. Si estás
buscando cómo levantar/desarrollar este backend, ver
[`README.md`](README.md) en esta misma carpeta.

## El contrato en una frase

Tu backend le manda un `POST /api/alerts` a Mobile Alert cada vez que
quiere avisarle a bomberos puntuales; Mobile Alert se encarga del push
(FCM) y, cuando alguien responde, te avisa a vos por un webhook. Nunca
tenés que consultarnos por polling.

```
Tu backend  ──POST /api/alerts (X-Api-Key)──▶  Mobile Alert  ──FCM──▶  Teléfono del bombero
    ▲                                                                          │
    └──────────────── POST a tu webhook (X-Signature) ◀── respuesta ──────────┘
```

Documentación interactiva y navegable de todos los endpoints (podés probar
requests reales desde el navegador): **`http://<host>:5080/scalar`**
(spec crudo en `/openapi/v1.json`).

## 0. Conseguir una API key

Hoy no hay un endpoint de auto-registro (a propósito, ver "Qué falta" en
`README.md`) — se coordina a mano y se carga directo en la base. Pedila al
equipo que administra este backend. La vas a usar en el header `X-Api-Key`
en los dos endpoints de abajo.

Una API key identifica a **tu institución** — todo lo que hagas con ella
(mandar alertas, registrar webhooks) queda scopeado a los bomberos de tu
propia institución. No podés mandarle una alerta a bomberos de otro
cuartel, ni enterarte de si un id existe en otro cuartel (ver la nota sobre
`unknownFirefighterIds` más abajo).

## 1. Saber qué bomberos existen (`firefighterIds`)

`POST /api/alerts` necesita **ids internos nuestros**, no un DNI ni un
legajo tuyo. La forma de conocerlos es que cada bombero se haya logueado
alguna vez en la app — el login (`POST /api/auth/login`, lo llama la app,
no vos) devuelve `firefighter.id` en la respuesta. Si tu institución usa
[login delegado](#login-delegado-opcional) (ver más abajo), tu propio
sistema de login recibe el usuario/contraseña real de cada bombero, así que
en algún momento vas a necesitar correlacionar tu propio identificador de
empleado con este `firefighter.id` — hoy no hay un endpoint para
"listarlos" desde tu lado; si te hace falta, es una conversación para tener
con el equipo de este backend.

## 2. Disparar una alerta

```bash
curl -X POST http://<host>:5080/api/alerts \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TU_API_KEY" \
  -d '{
    "correlationId": "b3f6c9e0-2f4a-4b8b-9a1e-8f2c1d3e4a5b",
    "title": "Incendio estructural",
    "message": "Se solicita apoyo urgente.",
    "address": "Av. Siempre Viva 742",
    "latitude": -32.89,
    "longitude": -68.84,
    "firefighterIds": [1, 2, 5]
  }'
```

| Campo | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `correlationId` | UUID | Sí | **Lo generás vos.** Es tu identificador del aviso — viaja hasta el teléfono y vuelve en el webhook de cada respuesta, así correlacionás sin depender de nuestro `alertId` interno. Ver [idempotencia](#idempotencia-y-reintentos) abajo. |
| `title` | string | Sí | |
| `message` | string | Sí | |
| `address` | string | No | Se muestra en la pantalla de alerta. |
| `latitude` / `longitude` | number | No | Si vienen los dos, la app calcula y muestra la distancia del bombero al siniestro. |
| `firefighterIds` | int[] | Sí, no vacío | Ids nuestros (ver punto 1), no los tuyos. |

### Respuesta — siempre 200

```json
{
  "alertId": "f1a2b3c4-...",
  "correlationId": "b3f6c9e0-...",
  "devicesNotified": 2,
  "unknownFirefighterIds": [7],
  "firefightersWithoutDevice": [5]
}
```

**Siempre es 200**, incluso si algunos (o todos) los `firefighterIds` no
pudieron recibir el push — se prioriza mandar lo que se pueda mandar en vez
de fallar todo por un id inválido. Revisá siempre estos dos campos, no solo
el status code:

- **`unknownFirefighterIds`**: el id no existe en tu institución (o existe
  en OTRA institución — no lo distinguimos a propósito, para no filtrarte
  si un id existe en algún lado que no es el tuyo).
- **`firefightersWithoutDevice`**: el id existe, pero ese bombero nunca
  abrió la app / nunca le dio permiso de notificaciones, así que no hay
  ningún token FCM al cual mandarle nada todavía.

`400` solo si el request está mal armado (falta `correlationId`, o
`firefighterIds` vacío/ausente) — no por fallos de entrega, esos van en el
body como arriba.

### Idempotencia y reintentos

Si tu sistema reintenta un POST que se colgó (timeout, no supiste si llegó
o no), mandalo de nuevo **con el mismo `correlationId`** — Mobile Alert
detecta el duplicado y devuelve la alerta ya creada sin volver a mandar el
push. Nunca generes un `correlationId` nuevo "por las dudas" en un retry.

Aparte de tus reintentos, Mobile Alert reintenta el push automáticamente
cada 10s (configurable) hasta que **algún** bombero responda o se agote el
máximo de reintentos (30 por default) — no tenés que implementar ningún
retry de tu lado más allá de la idempotencia de arriba.

## 3. Recibir las respuestas (webhook)

### Registrar tu URL

```bash
curl -X POST http://<host>:5080/api/webhooks \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: TU_API_KEY" \
  -d '{ "url": "https://tu-sistema.example.com/hooks/mobile-alert" }'
```

```json
{ "id": 3, "url": "https://tu-sistema.example.com/hooks/mobile-alert", "secret": "a1b2c3...(64 hex chars)" }
```

**Guardá `secret` ahora — se devuelve UNA sola vez, no hay forma de volver
a consultarlo.** Si lo perdiste, registrá una URL nueva (o pedí que se
regenere a mano).

### Lo que vas a recibir

Cada vez que un bombero de tu institución responde una alerta, te llega:

```http
POST /hooks/mobile-alert HTTP/1.1
Content-Type: application/json
X-Signature: 5e8f...(hex, HMAC-SHA256 del body con tu secret)

{
  "alertId": "f1a2b3c4-...",
  "correlationId": "b3f6c9e0-...",
  "firefighterId": 2,
  "response": "ATTENDING",
  "location": { "latitude": -32.89, "longitude": -68.84, "accuracy": 12.5 },
  "respondedAt": "2026-08-28T22:41:36Z"
}
```

`response` es `"ATTENDING"` o `"NOT_ATTENDING"`. `location` es `null` si el
bombero no dio permiso de ubicación o no se pudo obtener a tiempo. Usá
`correlationId` (el que vos generaste) para identificar de qué aviso
propio es esta respuesta — no `alertId`, que es nuestra PK interna.

### Verificar la firma

El header `X-Signature` es el HMAC-SHA256 (hex) del body **crudo, tal cual
llegó**, firmado con el `secret` que te devolvimos al registrar la URL.
Ejemplo en Node:

```js
const crypto = require('crypto');

function isValidSignature(rawBody, signatureHeader, secret) {
  const expected = crypto.createHmac('sha256', secret).update(rawBody).digest('hex');
  // timingSafeEqual, no === — comparar HMACs con === filtra tiempo de
  // ejecución que un atacante puede usar para adivinar la firma byte a byte.
  return crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(signatureHeader));
}
```

Y en C# / .NET:

```csharp
static bool IsValidSignature(string rawBody, string signatureHeader, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
}
```

La firma es justamente lo que te permite exponer este endpoint **sin auth
adicional** (ni API key, ni IP allowlist) — cualquiera puede pegarle a tu
URL, pero solo una request firmada con tu `secret` es de verdad nuestra.
Rechazá cualquier request sin firma válida.

### Qué pasa si tu endpoint está caído

Se loguea de nuestro lado y se sigue — no reintentamos webhooks fallidos
todavía (ver "Qué falta" en `README.md`). La respuesta del bombero **igual
queda guardada** en Mobile Alert aunque tu webhook nunca la reciba; si
necesitás recuperar algo que se perdió, `GET /api/alerts/{id}/responses`
(sin auth, pensado para debug manual) te la devuelve por polling como
último recurso.

## Login delegado (opcional)

Si preferís que tus bomberos sigan logueándose con SU usuario/contraseña
de tu sistema (en vez de que Mobile Alert les cree credenciales nuevas),
tu institución puede configurarse con un `LoginBackendUrl` propio — pedíselo
al equipo de este backend. El contrato que tu endpoint de login tiene que
cumplir:

- Recibe `POST { username, password }` (la contraseña real del bombero,
  tal cual la tipeó — por eso `LoginBackendUrl` tiene que ser HTTPS
  siempre en producción).
- Responde `200 { name: string, externalId?: string }` si son válidas,
  cualquier otro status si no.

Con esto configurado, Mobile Alert reenvía cada intento de login a tu
endpoint y confía en tu respuesta — no guarda ninguna contraseña de tus
bomberos.

## Aislamiento entre instituciones

Todo lo que hagas con tu API key queda scopeado a tu propia institución:
no podés mandar alertas a bomberos de otro cuartel, ni tu webhook recibe
respuestas de bomberos ajenos, ni un `firefighterId` de otra institución te
resuelve como válido (te aparece en `unknownFirefighterIds`, indistinguible
de un id que directamente no existe en ningún lado).
