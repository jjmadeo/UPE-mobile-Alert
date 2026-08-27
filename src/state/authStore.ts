import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { BrandingConfig, DEFAULT_BRANDING } from '../types/branding';

export interface Firefighter {
  id: string;
  name: string;
  username: string;
}

interface AuthState {
  token: string | null;
  firefighter: Firefighter | null;
  branding: BrandingConfig;
  /** Guardado para poder re-registrar el token FCM luego de un cold start,
   * antes de que el usuario haga login de nuevo (sesión persistida). */
  isAuthenticated: boolean;
  setSession: (params: {
    token: string;
    firefighter: Firefighter;
    branding: BrandingConfig;
  }) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    set => ({
      token: null,
      firefighter: null,
      branding: DEFAULT_BRANDING,
      isAuthenticated: false,
      setSession: ({ token, firefighter, branding }) =>
        set({ token, firefighter, branding, isAuthenticated: true }),
      logout: () =>
        set({
          token: null,
          firefighter: null,
          branding: DEFAULT_BRANDING,
          isAuthenticated: false,
        }),
    }),
    {
      name: 'mobile-alert-auth',
      storage: createJSONStorage(() => AsyncStorage),
    },
  ),
);
