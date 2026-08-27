import axios, { AxiosInstance } from 'axios';
import { MOCK_BACKEND_URL } from '../config/env';
import { useAuthStore } from '../state/authStore';

/**
 * Se crea una instancia nueva por request en lugar de una única instancia
 * global porque el baseURL (backend de la institución) y el token cambian
 * con el login/logout, y el volumen de requests de esta app es mínimo
 * (login + responder alerta). Así evitamos manejar interceptors con estado
 * mutable.
 */
export function getApiClient(): AxiosInstance {
  const { token, branding } = useAuthStore.getState();
  return axios.create({
    baseURL: branding.backendUrl || MOCK_BACKEND_URL,
    timeout: 15000,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
  });
}
