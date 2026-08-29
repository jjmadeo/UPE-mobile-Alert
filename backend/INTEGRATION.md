# Guía de integración

Esta guía describe cómo el sistema de despacho de una institución (cuartel)
puede integrarse con Mobile Alert para enviar alertas y recibir las
respuestas de sus bomberos. Para la ejecución y el funcionamiento interno
del backend, ver [`README.md`](README.md).

## Resumen del contrato

El sistema del cuartel envía una solicitud `POST /api/alerts` cada vez que
necesita notificar a bomberos puntuales. Mobile Alert gestiona el envío de
la notificación push y, cuando un bombero responde, notifica al sistema
del cuartel mediante un webhook. No es necesario realizar consultas
periódicas (polling).

![Diagrama de arquitectura de Mobile Alert](../docs/diagrams/arquitectura.svg)

Diagrama de arquitectura y de secuencia de cada caso de uso:
[Arquitectura y flujos](../docs/architecture.html).

Documentación interactiva de la API, con posibilidad de ejecutar
solicitudes desde el navegador: `http://<host>:5080/scalar` (documento
OpenAPI en `/openapi/v1.json`).

### Endpoints de esta integración

| Método | Ruta | Autenticación | Uso |
|---|---|---|---|
| POST | `/api/alerts` | `X-Api-Key` | Disparar una alerta a bomberos puntuales |
| POST | `/api/webhooks` | `X-Api-Key` | Registrar la URL propia que recibe las respuestas |

Ningún otro endpoint del backend es parte del contrato de integración: el
resto (login, registro de dispositivo, respuesta a una alerta) lo consume
la aplicación móvil, no el sistema del cuartel.

### Requisitos

- `Content-Type: application/json` en toda solicitud con cuerpo.
- HTTPS obligatorio en producción para la URL del webhook (paso 4) y para
  el endpoint de autenticación delegada (opcional, ver más abajo); el
  ejemplo con `http://<host>:5080` es solo para probar en red local.
- No hay límite de tasa (*rate limit*) propio sobre estos endpoints
  actualmente.

## 1. Obtención de una clave de API

El registro de claves de API no está automatizado; debe coordinarse con el
equipo responsable del backend. La clave se utiliza en el encabezado
`X-Api-Key` de los endpoints descritos en esta guía.

Una clave de API identifica a una institución. Toda operación realizada
con ella (envío de alertas, registro de webhooks) queda restringida a los
bomberos de esa institución. No es posible enviar alertas a bomberos de
otra institución, ni determinar si un identificador pertenece a otra
institución (ver `unknownFirefighterIds` más abajo).

## 2. Identificación de bomberos (`firefighterIds`)

`POST /api/alerts` requiere identificadores internos de Mobile Alert, no
identificadores propios del sistema del cuartel. Estos identificadores se
obtienen del inicio de sesión de cada bombero en la aplicación móvil
(`firefighter.id` en la respuesta de `POST /api/auth/login`).

En instituciones con [autenticación delegada](#autenticación-delegada-opcional),
el sistema del cuartel recibe las credenciales originales de cada bombero
y debe correlacionar su propio identificador de empleado con el
`firefighter.id` asignado por Mobile Alert. No existe actualmente un
endpoint para consultar este mapeo de forma directa.

## 3. Envío de una alerta

![Diagrama de secuencia: creación y envío de una alerta](../docs/diagrams/alerta.svg)

```bash
curl -X POST http://<host>:5080/api/alerts \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <clave-de-api>" \
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

| Campo | Tipo | Obligatorio | Descripción |
|---|---|---|---|
| `correlationId` | UUID | Sí | Identificador generado por el sistema del cuartel. Se retorna en el webhook de cada respuesta, y permite reenvíos idempotentes (ver más abajo). |
| `title` | string | Sí | |
| `message` | string | Sí | |
| `address` | string | No | Se muestra en la pantalla de alerta. |
| `latitude` / `longitude` | number | No | Si ambos están presentes, la aplicación calcula y muestra la distancia del bombero al incidente. |
| `firefighterIds` | int[] | Sí | Identificadores de Mobile Alert (ver sección 2). |

### Respuesta

```json
{
  "alertId": "f1a2b3c4-...",
  "correlationId": "b3f6c9e0-...",
  "devicesNotified": 2,
  "unknownFirefighterIds": [7],
  "firefightersWithoutDevice": [5]
}
```

La respuesta es `200` aun cuando parte de los destinatarios no reciban la
notificación: el envío se realiza a los destinatarios válidos, y el
detalle de las excepciones se informa en el cuerpo de la respuesta.

- `unknownFirefighterIds`: el identificador no existe en la institución
  (o pertenece a otra institución; ambos casos son indistinguibles por
  diseño).
- `firefightersWithoutDevice`: el identificador existe, pero no tiene un
  dispositivo registrado.

Un `400` indica una solicitud mal formada (por ejemplo, ausencia de
`correlationId` o `firefighterIds` vacío), no una falla de entrega.

### Idempotencia y reintentos

Un reenvío de la misma solicitud debe conservar el `correlationId`
original. Mobile Alert detecta la repetición y devuelve la alerta ya
registrada, sin reenviar la notificación.

Independientemente de los reintentos del sistema del cuartel, Mobile Alert
reintenta el envío de la notificación automáticamente (cada 10 segundos
por defecto) hasta que un bombero responda o se alcance el máximo de
reintentos configurado (30 por defecto).

## 4. Recepción de respuestas mediante webhook

![Diagrama de secuencia: registro de webhook](../docs/diagrams/webhook.svg)

### Registro de la URL de destino

```bash
curl -X POST http://<host>:5080/api/webhooks \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <clave-de-api>" \
  -d '{ "url": "https://sistema-del-cuartel.example.com/hooks/mobile-alert" }'
```

```json
{ "id": 3, "url": "https://sistema-del-cuartel.example.com/hooks/mobile-alert", "secret": "a1b2c3... (64 caracteres hexadecimales)" }
```

El campo `secret` se retorna una única vez, en esta respuesta. No existe
un mecanismo para recuperarlo posteriormente; en caso de pérdida, debe
registrarse una nueva suscripción.

### Formato de la notificación

![Diagrama de secuencia: respuesta a una alerta y notificación al cuartel](../docs/diagrams/respuesta.svg)

Cada vez que un bombero de la institución responde una alerta, se envía:

```http
POST /hooks/mobile-alert HTTP/1.1
Content-Type: application/json
X-Signature: 5e8f... (HMAC-SHA256 del cuerpo, en hexadecimal)

{
  "alertId": "f1a2b3c4-...",
  "correlationId": "b3f6c9e0-...",
  "firefighterId": 2,
  "response": "ATTENDING",
  "location": { "latitude": -32.89, "longitude": -68.84, "accuracy": 12.5 },
  "respondedAt": "2026-08-28T22:41:36Z"
}
```

`response` toma los valores `ATTENDING` o `NOT_ATTENDING`. `location` es
`null` cuando el bombero no otorgó permiso de ubicación o esta no pudo
obtenerse a tiempo. El campo `correlationId` (no `alertId`, que es un
identificador interno de Mobile Alert) debe utilizarse para asociar la
respuesta con la alerta original.

### Verificación de la firma

El encabezado `X-Signature` corresponde al HMAC-SHA256, en hexadecimal,
del cuerpo de la solicitud sin modificar, calculado con el `secret`
obtenido al registrar la URL.

Ejemplo en Node.js:

```js
const crypto = require('crypto');

function isValidSignature(rawBody, signatureHeader, secret) {
  const expected = crypto.createHmac('sha256', secret).update(rawBody).digest('hex');
  return crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(signatureHeader));
}
```

Ejemplo en C# / .NET:

```csharp
static bool IsValidSignature(string rawBody, string signatureHeader, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
}
```

La verificación de la firma es el mecanismo que permite exponer este
endpoint sin autenticación adicional. Toda solicitud sin firma válida
debe rechazarse.

### Comportamiento ante fallas de entrega

Si la URL de destino no responde, el intento queda registrado y **no se
reintenta automáticamente**. La respuesta del bombero se conserva en
Mobile Alert independientemente del resultado de la entrega del webhook,
pero no existe todavía un endpoint autenticado para que el cuartel la
recupere manualmente en ese caso; el sistema receptor del webhook debe
responder rápido y con un código `2xx` para minimizar la ventana de
pérdida, y monitorear su propia disponibilidad. Ante una falla persistente,
contactar al equipo responsable del backend.

## Autenticación delegada (opcional)

Una institución puede configurarse para que sus bomberos continúen
utilizando las credenciales de su propio sistema, en lugar de credenciales
gestionadas por Mobile Alert. Esta configuración requiere coordinación con
el equipo responsable del backend.

El endpoint de autenticación de la institución debe cumplir el siguiente
contrato:

- Recibir `POST { username, password }` con las credenciales originales
  del bombero. Por este motivo, la URL configurada debe utilizar HTTPS en
  todo entorno de producción.
- Responder `200 { name: string, externalId?: string }` ante credenciales
  válidas, o cualquier otro código de estado en caso contrario.

Bajo esta configuración, Mobile Alert reenvía cada intento de inicio de
sesión al endpoint de la institución sin almacenar contraseñas.

## Códigos de estado

| Código | Endpoint | Significado |
|---|---|---|
| `200` | `POST /api/alerts` | Solicitud aceptada, incluso si algún destinatario quedó fuera (ver `unknownFirefighterIds` / `firefightersWithoutDevice`). |
| `200` | `POST /api/webhooks` | Suscripción creada. |
| `400` | `POST /api/alerts` | Solicitud mal formada (`correlationId` ausente, `firefighterIds` vacío, etc.), no una falla de entrega. |
| `400` | `POST /api/webhooks` | `url` ausente o no es una URL `http(s)` absoluta. |
| `401` | Cualquiera | Falta el header `X-Api-Key`, o la clave no es válida. |

Todo error incluye un cuerpo `{ "message": "..." }` con el detalle.

## Aislamiento entre instituciones

Toda operación realizada con una clave de API queda restringida a la
institución asociada. No es posible enviar alertas a bomberos de otra
institución, recibir respuestas de bomberos ajenos, ni validar
identificadores pertenecientes a otra institución.
