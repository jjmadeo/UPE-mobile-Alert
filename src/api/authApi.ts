import axios from 'axios';
import { BrandingConfig } from '../types/branding';
import { Firefighter } from '../state/authStore';
import { useServerConfigStore } from '../state/serverConfigStore';
import { getApiClient } from './client';

export interface LoginParams {
  institutionCode: string;
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  firefighter: Firefighter;
  branding: BrandingConfig;
}

/**
 * El login pega siempre al backend "general" configurado en
 * serverConfigStore (no al backendUrl de la institución, que todavía no
 * conocemos) porque es el que resuelve qué institución es cada código y
 * devuelve su branding + backendUrl real.
 */
export async function login(params: LoginParams): Promise<LoginResponse> {
  const { serverUrl } = useServerConfigStore.getState();
  const { data } = await axios.post<LoginResponse>(
    `${serverUrl}/api/auth/login`,
    params,
  );
  return data;
}

export async function registerDeviceToken(fcmToken: string): Promise<void> {
  await getApiClient().post('/api/devices/register', { fcmToken });
}
