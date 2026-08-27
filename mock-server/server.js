/**
 * Mock backend para probar la app end-to-end mientras no existe el backend
 * real. Implementa el mismo contrato que debería exponer el backend
 * definitivo: /api/auth/login, /api/devices/register y
 * /api/alerts/:id/response. Todo en memoria, se resetea al reiniciar.
 *
 * Correr con: npm run mock-server (desde la raíz del repo)
 */
const express = require('express');
const cors = require('cors');

const PORT = process.env.PORT || 4000;

// Host visto desde la app tras el login (branding.backendUrl). Debe
// coincidir con MOCK_BACKEND_URL en src/config/env.ts. Default: gateway de
// la red bridge de Docker (emulador corriendo en budtmo/docker-android).
// Para Android Studio, usar 10.0.2.2; para dispositivo físico, la IP LAN.
const BACKEND_HOST = process.env.BACKEND_HOST || '172.17.0.1';

// --- "Base de datos" en memoria --------------------------------------------

const INSTITUTIONS = {
  'BOMBEROS-CENTRAL': {
    institutionCode: 'BOMBEROS-CENTRAL',
    institutionName: 'Bomberos Voluntarios Central',
    primaryColor: '#1E3A8A',
    backendUrl: `http://${BACKEND_HOST}:${PORT}`,
  },
  'BOMBEROS-NORTE': {
    institutionCode: 'BOMBEROS-NORTE',
    institutionName: 'Bomberos Voluntarios Zona Norte',
    primaryColor: '#B45309',
    backendUrl: `http://${BACKEND_HOST}:${PORT}`,
  },
};

const USERS = [
  {
    id: 'ff-1',
    username: 'juan',
    password: '1234',
    name: 'Juan Pérez',
    institutionCode: 'BOMBEROS-CENTRAL',
  },
  {
    id: 'ff-2',
    username: 'maria',
    password: '1234',
    name: 'María Gómez',
    institutionCode: 'BOMBEROS-NORTE',
  },
];

/** token -> { firefighterId } */
const sessions = new Map();
/** firefighterId -> { fcmToken, registeredAt } */
const devices = new Map();
/** lista de respuestas recibidas, solo para debug/demo */
const alertResponses = [];

function generateToken() {
  return `mock-token-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function authMiddleware(req, res, next) {
  const header = req.headers.authorization ?? '';
  const token = header.startsWith('Bearer ') ? header.slice(7) : null;
  const session = token ? sessions.get(token) : null;
  if (!session) {
    return res.status(401).json({ message: 'Token inválido o ausente.' });
  }
  req.firefighterId = session.firefighterId;
  next();
}

// --- App ---------------------------------------------------------------

const app = express();
app.use(cors());
app.use(express.json());

app.get('/api/health', (_req, res) => res.json({ ok: true }));

app.post('/api/auth/login', (req, res) => {
  const { institutionCode, username, password } = req.body ?? {};

  const institution = INSTITUTIONS[institutionCode];
  if (!institution) {
    return res.status(404).json({ message: 'Institución no encontrada.' });
  }

  const user = USERS.find(
    u =>
      u.institutionCode === institutionCode &&
      u.username === username &&
      u.password === password,
  );
  if (!user) {
    return res.status(401).json({ message: 'Usuario o contraseña incorrectos.' });
  }

  const token = generateToken();
  sessions.set(token, { firefighterId: user.id });

  console.log(`[login] ${user.name} (${institutionCode}) -> token ${token}`);

  res.json({
    token,
    firefighter: { id: user.id, name: user.name, username: user.username },
    branding: institution,
  });
});

app.post('/api/devices/register', authMiddleware, (req, res) => {
  const { fcmToken } = req.body ?? {};
  if (!fcmToken) {
    return res.status(400).json({ message: 'Falta fcmToken.' });
  }
  devices.set(req.firefighterId, { fcmToken, registeredAt: new Date().toISOString() });
  console.log(`[devices] ${req.firefighterId} -> ${fcmToken}`);
  res.status(204).end();
});

/** Debug: usado por mock-server/send-test-alert.js para no tener que copiar
 * el token del dispositivo a mano. Devuelve el último token registrado. */
app.get('/api/devices/latest', (_req, res) => {
  const entries = [...devices.entries()];
  if (entries.length === 0) {
    return res.status(404).json({ message: 'Todavía no se registró ningún dispositivo.' });
  }
  const [firefighterId, device] = entries[entries.length - 1];
  res.json({ firefighterId, ...device });
});

app.post('/api/alerts/:alertId/response', authMiddleware, (req, res) => {
  const { alertId } = req.params;
  const { response, location, respondedAt } = req.body ?? {};

  if (response !== 'ATTENDING' && response !== 'NOT_ATTENDING') {
    return res.status(400).json({ message: 'response inválido.' });
  }

  const entry = { alertId, firefighterId: req.firefighterId, response, location, respondedAt };
  alertResponses.push(entry);

  const emoji = response === 'ATTENDING' ? '✅ ASISTE' : '❌ NO ASISTE';
  const loc = location ? `(${location.latitude}, ${location.longitude})` : '(sin ubicación)';
  console.log(`[response] alerta=${alertId} bombero=${req.firefighterId} ${emoji} ${loc}`);

  res.json({ ok: true });
});

/** Debug: ver todas las respuestas recibidas para una alerta, sin auth para
 * poder chequearlo fácil desde el navegador durante pruebas. */
app.get('/api/alerts/:alertId/responses', (req, res) => {
  res.json(alertResponses.filter(r => r.alertId === req.params.alertId));
});

app.listen(PORT, () => {
  console.log(`Mock backend escuchando en http://localhost:${PORT}`);
  console.log('Usuarios de prueba:');
  USERS.forEach(u =>
    console.log(`  - institución=${u.institutionCode} usuario=${u.username} password=${u.password}`),
  );
});
