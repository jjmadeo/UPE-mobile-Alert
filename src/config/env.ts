/**
 * URL del backend contra el que loguea la app (ver /mock-server para el
 * mock viejo, o /backend para el real en C#/Postgres — ahora mismo apunta
 * al real, puerto 5080; `docker compose up -d` en backend/ para levantarlo).
 *
 * - Emulador de Android Studio: 10.0.2.2 apunta al localhost de la máquina host.
 * - Emulador corriendo en Docker (budtmo/docker-android): el contenedor está
 *   en la red bridge de Docker, así que el host se ve en el gateway de esa
 *   red (172.17.0.1 por defecto — confirmar con
 *   `docker inspect <container> --format '{{json .NetworkSettings.Networks}}'`).
 * - Dispositivo físico: reemplazar por la IP de tu PC en la red local
 *   (ej: 'http://192.168.1.50:5080') o por la URL del backend real en prod.
 *
 * En producción, este valor se reemplaza en tiempo de login: el backend
 * devuelve `backendUrl` dentro del branding de cada institución (ver
 * BrandingConfig) y la app usa ese valor a partir de ahí — hoy
 * backend/Services/AuthService.cs manda ese campo vacío a propósito (no hay
 * todavía un backendUrl por institución distinto de este mismo backend), así
 * que todo — login y llamadas autenticadas — sigue cayendo acá.
 */
export const MOCK_BACKEND_URL = 'http://172.17.0.1:5080';

export const NOTIFICATION_CHANNEL_ID = 'bomberos-alertas';
export const NOTIFICATION_CHANNEL_NAME = 'Alertas de emergencia';
