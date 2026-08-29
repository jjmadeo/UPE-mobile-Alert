import axios, { AxiosInstance } from 'axios';
import { useAuthStore } from '../state/authStore';
import { useServerConfigStore } from '../state/serverConfigStore';

/**
 * Se crea una instancia nueva por request en lugar de una única instancia
 * global porque el baseURL (backend de la institución) y el token cambian
 * con el login/logout, y el volumen de requests de esta app es mínimo
 * (login + responder alerta). Así evitamos manejar interceptors con estado
 * mutable.
 */
export function getApiClient(): AxiosInstance {
  const { token, branding } = useAuthStore.getState();
  const { serverUrl } = useServerConfigStore.getState();
  return axios.create({
    baseURL: branding.backendUrl || serverUrl,
    timeout: 15000,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
}
