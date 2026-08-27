import axios from 'axios';
import { MOCK_BACKEND_URL } from '../config/env';
import { BrandingConfig } from '../types/branding';
import { Firefighter } from '../state/authStore';
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
 * El login pega siempre al mock-server "general" (no al backendUrl de la
 * institución, que todavía no conocemos) porque es el que resuelve qué
 * institución es cada código y devuelve su branding + backendUrl real.
 */
export async function login(params: LoginParams): Promise<LoginResponse> {
  const { data } = await axios.post<LoginResponse>(
    `${MOCK_BACKEND_URL}/api/auth/login`,
    params,
  );
  return data;
}

export async function registerDeviceToken(fcmToken: string): Promise<void> {
  await getApiClient().post('/api/devices/register', { fcmToken });
}
