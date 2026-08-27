/**
 * Simula al backend PROPIO de un cuartel disparando una alerta real contra
 * nuestro backend (backend/), autenticado con una API key — a diferencia
 * de mock-server/send-test-alert.js, que le pega directo a Firebase
 * salteándose todo el backend. Este es el que hay que usar para probar
 * backend/ de punta a punta (fan-out, retry, webhooks, todo lo que vive
 * ahí).
 *
 * Requiere: backend/ corriendo (`docker compose up -d` en backend/) y la
 * app logueada al menos una vez contra ese mismo backend (para que el
 * device token quede registrado — ver src/config/env.ts, MOCK_BACKEND_URL
 * tiene que apuntar acá, no al mock-server).
 *
 * Uso:
 *   node backend/scripts/send-test-alert.js
 *   node backend/scripts/send-test-alert.js --title="Incendio" --message="Depósito de químicos" --address="Av. Siempre Viva 742"
 *   node backend/scripts/send-test-alert.js --firefighter-ids=1,2       # a mano, si ya sabés los ids
 *   node backend/scripts/send-test-alert.js --api-key=<otra key>
 *   node backend/scripts/send-test-alert.js --lat=-32.89 --lng=-68.84
 */
const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:5080';
const DEFAULT_API_KEY = 'demo-central-CAMBIAR-EN-SERIO-esto-es-solo-para-dev';
const DEFAULT_INSTITUTION_CODE = 'BOMBEROS-CENTRAL';
const DEFAULT_USERNAME = 'juan';
const DEFAULT_PASSWORD = '1234';

function parseArgs() {
  const args = {};
  for (const arg of process.argv.slice(2)) {
    const withValue = arg.match(/^--([^=]+)=(.*)$/);
    if (withValue) {
      args[withValue[1]] = withValue[2];
      continue;
    }
    const bare = arg.match(/^--(.+)$/);
    if (bare) {
      args[bare[1]] = true;
    }
  }
  return args;
}

/** Sin --firefighter-ids, resuelve el id del usuario de prueba
 * logueándose — así el script sigue andando aunque se resetee la base
 * (los ids autoincrementales pueden cambiar). */
async function resolveDefaultFirefighterId() {
  const res = await fetch(`${BACKEND_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      institutionCode: DEFAULT_INSTITUTION_CODE,
      username: DEFAULT_USERNAME,
      password: DEFAULT_PASSWORD,
    }),
  });
  if (!res.ok) {
    throw new Error(
      `No se pudo loguear como ${DEFAULT_USERNAME} para resolver su firefighterId ` +
        `(${res.status}). Pasá --firefighter-ids=<id,id,...> a mano, o revisá que ` +
        `backend/ esté corriendo en ${BACKEND_URL}.`,
    );
  }
  const data = await res.json();
  // firefighter.id viaja como string en el JSON del login (FirefighterDto.Id
  // es string, para no atarse al tipo de id real del cuartel delegado — ver
  // AuthService.cs) pero CreateAlertRequestDto.FirefighterIds es int[]: hay
  // que convertir, si no System.Text.Json rechaza el body con 400.
  return Number(data.firefighter.id);
}

async function main() {
  const args = parseArgs();
  const apiKey = args['api-key'] || DEFAULT_API_KEY;

  const firefighterIds = args['firefighter-ids']
    ? args['firefighter-ids'].split(',').map(Number)
    : [await resolveDefaultFirefighterId()];

  const correlationId = args['correlation-id'] || crypto.randomUUID();

  const body = {
    correlationId,
    title: args.title || 'Incendio estructural',
    message: args.message || 'Se solicita apoyo urgente.',
    address: args.address || 'Av. Siempre Viva 742',
    latitude: args.lat !== undefined ? Number(args.lat) : undefined,
    longitude: args.lng !== undefined ? Number(args.lng) : undefined,
    firefighterIds,
  };

  console.log(`POST ${BACKEND_URL}/api/alerts`);
  console.log('firefighterIds:', firefighterIds);
  console.log('correlationId:', correlationId);

  const res = await fetch(`${BACKEND_URL}/api/alerts`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': apiKey,
    },
    body: JSON.stringify(body),
  });

  const data = await res.json();
  console.log(`\nstatus: ${res.status}`);
  console.log(JSON.stringify(data, null, 2));

  if (data.unknownFirefighterIds?.length) {
    console.warn(`\n⚠️  ids que no existen en la institución: ${data.unknownFirefighterIds}`);
  }
  if (data.firefightersWithoutDevice?.length) {
    console.warn(`⚠️  ids sin ningún device token registrado: ${data.firefightersWithoutDevice}`);
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
