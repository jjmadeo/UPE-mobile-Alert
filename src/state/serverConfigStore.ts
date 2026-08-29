import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { MOCK_BACKEND_URL } from '../config/env';

/**
 * URL del backend, configurable EN RUNTIME (no solo al compilar) — para que
 * el mismo APK sirva sin tener que recompilar por cada red distinta. El
 * caso de uso concreto: alguien clona el repo, levanta `backend/` con
 * `docker compose up -d` en su propia PC, instala el APK ya compilado
 * (ver /dist) en un celular en la misma red, y en el login le pone la IP de
 * SU PC — no la de quien compiló el APK originalmente.
 *
 * `MOCK_BACKEND_URL` (src/config/env.ts) sigue existiendo como valor
 * default al compilar, para no romper el flujo de desarrollo local con
 * emulador — esto solo se usa como semilla inicial de este store; una vez
 * que el usuario la cambia, se persiste y gana siempre a partir de ahí.
 */
interface ServerConfigState {
  serverUrl: string;
  setServerUrl: (url: string) => void;
}

export const useServerConfigStore = create<ServerConfigState>()(
  persist(
    set => ({
      serverUrl: MOCK_BACKEND_URL,
      setServerUrl: url => set({ serverUrl: url.trim().replace(/\/+$/, '') }),
    }),
    {
      name: 'mobile-alert-server-config',
      storage: createJSONStorage(() => AsyncStorage),
    },
  ),
);
