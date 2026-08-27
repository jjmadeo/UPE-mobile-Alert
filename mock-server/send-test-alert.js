/**
 * Simula lo que haría el backend real: dispara una alerta por FCM a un
 * dispositivo, y sigue insistiendo (reenvía la misma alerta cada N segundos)
 * hasta que el bombero responda — así el teléfono no se queda en un solo
 * push silencioso si nadie la vio a tiempo. Requiere:
 *
 *   1. mock-server/serviceAccountKey.json — se descarga desde la consola de
 *      Firebase: Configuración del proyecto > Cuentas de servicio >
 *      "Generar nueva clave privada". NO se commitea (ver .gitignore).
 *   2. Un dispositivo con la app instalada y logueada al menos una vez
 *      (para que el token FCM haya quedado registrado en el mock server).
 *
 * Uso:
 *   node mock-server/send-test-alert.js
 *   node mock-server/send-test-alert.js --token=<fcmToken explícito>
 *   node mock-server/send-test-alert.js --title="Incendio" --message="Depósito de químicos" --address="Av. Siempre Viva 742"
 *   node mock-server/send-test-alert.js --no-repeat                 # un solo envío, sin insistir
 *   node mock-server/send-test-alert.js --repeat-interval=5         # insistir cada 5s en vez de 10s
 *   node mock-server/send-test-alert.js --max-repeats=10            # tope de reintentos (default 30 ≈ 5 min)
 *   node mock-server/send-test-alert.js --lat=37.45 --lng=-122.08   # coordenadas del siniestro (para la distancia en pantalla)
 *   node mock-server/send-test-alert.js --no-location               # no mandar coordenadas (sin default)
 *   node mock-server/send-test-alert.js --correlation-id=<uuid>     # simula el id que generaría el backend del cuartel (default: al azar)
 *
 * NOTA para el backend real (cuando exista, ver README "Qué falta para
 * producción"): esta misma idea — re-mandar el push cada N segundos hasta
 * que llegue una respuesta para ese alertId — es la que tiene que vivir ahí,
 * en el módulo de fan-out. Acá vive en el script de prueba porque hoy no hay
 * otro lugar donde ponerla.
 */
const path = require('path');
const fs = require('fs');

const SERVICE_ACCOUNT_PATH = path.join(__dirname, 'serviceAccountKey.json');
const MOCK_SERVER_URL = process.env.MOCK_SERVER_URL || 'http://localhost:4000';
const DEFAULT_REPEAT_INTERVAL_SECONDS = 10;
const DEFAULT_MAX_REPEATS = 30;
// ~3.3 km al norte de la ubicación mock que devuelve el emulador de Android
// (37.4219983, -122.084, Mountain View) — así el default ya muestra una
// distancia razonable en pantalla sin tener que pasar --lat/--lng a mano.
const DEFAULT_LATITUDE = 37.4519983;
const DEFAULT_LONGITUDE = -122.084;

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function parseArgs() {
  const args = {};
  for (const arg of process.argv.slice(2)) {
    const withValue = arg.match(/^--([^=]+)=(.*)$/);
    if (withValue) {
      args[withValue[1]] = withValue[2];
      continue;
    }
    // Flag booleano sin valor, ej. --no-repeat.
    const bare = arg.match(/^--(.+)$/);
    if (bare) {
      args[bare[1]] = true;
    }
  }
  return args;
}

async function resolveToken(explicitToken) {
  if (explicitToken) {
    return explicitToken;
  }
  const res = await fetch(`${MOCK_SERVER_URL}/api/devices/latest`);
  if (!res.ok) {
    throw new Error(
      'No hay ningún dispositivo registrado todavía. Abrí la app, logueate, ' +
        'y esperá a que quede registrado el token (o pasá --token=... a mano).',
    );
  }
  const data = await res.json();
  return data.fcmToken;
}

/** true si ya hay al menos una respuesta registrada para este alertId. */
async function hasResponse(alertId) {
  const res = await fetch(`${MOCK_SERVER_URL}/api/alerts/${alertId}/responses`);
  if (!res.ok) {
    return false;
  }
  const responses = await res.json();
  return Array.isArray(responses) && responses.length > 0;
}

async function main() {
  if (!fs.existsSync(SERVICE_ACCOUNT_PATH)) {
    console.error(
      `Falta mock-server/serviceAccountKey.json.\n` +
        `Descargalo desde Firebase Console > Configuración del proyecto > ` +
        `Cuentas de servicio > "Generar nueva clave privada", y guardalo en esa ruta.`,
    );
    process.exit(1);
  }

  const admin = require('firebase-admin');
  admin.initializeApp({
    credential: admin.credential.cert(require(SERVICE_ACCOUNT_PATH)),
  });

  const args = parseArgs();
  const token = await resolveToken(args.token);

  const alertId = args.id || `test-${Date.now()}`;
  // Simula el id que en el backend real (backend/) genera el CUARTEL, no
  // nosotros — ver CreateAlertRequestDto.CorrelationId. Acá no hay ningún
  // cuartel de verdad detrás, así que si no se pasa uno, se genera al azar.
  const correlationId = args['correlation-id'] || require('crypto').randomUUID();

  // Coordenadas del siniestro: por default las de ejemplo de arriba, salvo
  // que pidan explícitamente no mandar ninguna (--no-location) — así se
  // puede seguir probando el caso "el backend no mandó ubicación" (la app
  // no debe mostrar distancia ni romperse, ver AlertScreen.tsx).
  const hasLocation = !('no-location' in args);
  const latitude = hasLocation
    ? Number(args.lat ?? DEFAULT_LATITUDE)
    : undefined;
  const longitude = hasLocation
    ? Number(args.lng ?? DEFAULT_LONGITUDE)
    : undefined;

  // Mismo alertId en cada reenvío a propósito: para notifee/Android es una
  // actualización de LA MISMA notificación (mismo `id: alert.id` en
  // displayAlertNotification), no una nueva apilada — pero sigue re-sonando
  // y re-vibrando en cada `notify()`, y si el teléfono está bloqueado en
  // ese momento, también vuelve a disparar el full-screen intent.
  const message = {
    token,
    // A propósito SIN campo `notification`: si lo incluyéramos, Android
    // mostraría su propia notificación default apenas la app esté en
    // background y nunca llegaría a ejecutarse nuestro handler de JS (el
    // que dispara la pantalla full-screen). Con solo `data`, siempre pasa
    // por src/notifications/fcm.ts.
    data: {
      alertId,
      correlationId,
      title: args.title || 'Incendio estructural',
      message: args.message || 'Se solicita apoyo urgente.',
      address: args.address || 'Av. Siempre Viva 742',
      ...(latitude !== undefined ? { latitude: String(latitude) } : {}),
      ...(longitude !== undefined ? { longitude: String(longitude) } : {}),
      createdAt: new Date().toISOString(),
    },
    android: {
      priority: 'high',
    },
    apns: {
      headers: { 'apns-priority': '10' },
      payload: { aps: { 'content-available': 1 } },
    },
  };

  async function sendOnce() {
    const response = await admin.messaging().send(message);
    console.log('Alerta enviada:', response);
  }

  await sendOnce();
  console.log('alertId:', alertId);
  console.log('correlationId:', correlationId);

  if ('no-repeat' in args) {
    return;
  }

  const intervalMs =
    (Number(args['repeat-interval']) || DEFAULT_REPEAT_INTERVAL_SECONDS) * 1000;
  const maxRepeats = Number(args['max-repeats']) || DEFAULT_MAX_REPEATS;

  console.log(
    `Insistiendo cada ${intervalMs / 1000}s hasta que respondan ` +
      `(máx ${maxRepeats} veces, Ctrl+C para cortar, --no-repeat para desactivar)...`,
  );
  for (let attempt = 1; attempt <= maxRepeats; attempt++) {
    await sleep(intervalMs);
    if (await hasResponse(alertId)) {
      console.log('✅ El bombero respondió — dejo de insistir.');
      return;
    }
    console.log(`⏰ Sin respuesta todavía, reenviando (intento ${attempt + 1})...`);
    await sendOnce();
  }
  console.log('⚠️ Se alcanzó el máximo de reintentos sin respuesta. Corto acá.');
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
