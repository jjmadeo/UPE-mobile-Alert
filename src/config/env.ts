/**
 * Valor SEMILLA de la URL del backend (puerto 5080; `docker compose up -d`
 * en backend/ para levantarlo) — solo se usa para inicializar
 * `serverConfigStore` la primera vez que arranca la app. A partir de ahí,
 * lo que manda es lo que haya en ese store (editable desde LoginScreen,
 * "Servidor") — así el MISMO APK compilado sirve en cualquier red sin
 * recompilar: cada quien lo cambia por la IP de SU PC.
 *
 * Referencia para saber qué poner acá (o en el campo "Servidor" de la app):
 * - Emulador de Android Studio: 10.0.2.2 apunta al localhost de la máquina host.
 * - Emulador corriendo en Docker (budtmo/docker-android): el contenedor está
 *   en la red bridge de Docker, así que el host se ve en el gateway de esa
 *   red (172.17.0.1 por defecto — confirmar con
 *   `docker inspect <container> --format '{{json .NetworkSettings.Networks}}'`).
 * - Dispositivo físico en la misma red WiFi que el backend: la IP de esa PC
 *   en la red local (`ip addr` en Linux/Mac, `ipconfig` en Windows — ver
 *   README para el paso a paso).
 *
 * En producción, este valor se reemplaza en tiempo de login: el backend
 * devuelve `backendUrl` dentro del branding de cada institución (ver
 * BrandingConfig) y la app usa ese valor a partir de ahí — hoy
 * backend/Services/AuthService.cs manda ese campo vacío a propósito (no hay
 * todavía un backendUrl por institución distinto de este mismo backend), así
 * que todo — login y llamadas autenticadas — sigue cayendo en el valor de
 * serverConfigStore.
 */
export const MOCK_BACKEND_URL = 'http://192.168.0.28:5080';

export const NOTIFICATION_CHANNEL_ID = 'bomberos-alertas';
export const NOTIFICATION_CHANNEL_NAME = 'Alertas de emergencia';
