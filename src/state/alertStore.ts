import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { AlertPayload, AlertResponseType } from '../types/alert';

export interface AnsweredAlert {
  alert: AlertPayload;
  response: AlertResponseType;
  respondedAt: string;
}

interface AlertState {
  /** Aviso actualmente mostrado en la pantalla roja (null = no hay ninguno
   * activo). Lo setea el handler de notificaciones al recibir/tocar un
   * push, y lo lee AlertScreen / la navegación para decidir si mostrarla. */
  currentAlert: AlertPayload | null;
  /** Historial local de últimas respuestas, solo para mostrar en HomeScreen. */
  history: AnsweredAlert[];
  showAlert: (alert: AlertPayload) => void;
  clearCurrentAlert: () => void;
  recordResponse: (alert: AlertPayload, response: AlertResponseType) => void;
}

const MAX_HISTORY = 20;

export const useAlertStore = create<AlertState>()(
  persist(
    (set, get) => ({
      currentAlert: null,
      history: [],
      showAlert: alert => set({ currentAlert: alert }),
      clearCurrentAlert: () => set({ currentAlert: null }),
      recordResponse: (alert, response) => {
        const entry: AnsweredAlert = {
          alert,
          response,
          respondedAt: new Date().toISOString(),
        };
        set({
          history: [entry, ...get().history].slice(0, MAX_HISTORY),
          currentAlert: null,
        });
      },
    }),
    {
      name: 'mobile-alert-history',
      storage: createJSONStorage(() => AsyncStorage),
      // `currentAlert` también se persiste a propósito: si el bombero
      // minimiza o mata la app sin responder, el aviso activo no tiene que
      // desaparecer solo — tiene que seguir ahí, esperando respuesta, la
      // próxima vez que se abra la app. Perder de vista un aviso sin
      // contestar es peor que mostrar uno viejo (el bombero ve la hora y
      // decide); es la misma lógica que ya aplicamos en
      // mock-server/send-test-alert.js insistiendo hasta que responda.
      partialize: state => ({
        history: state.history,
        currentAlert: state.currentAlert,
      }),
    },
  ),
);
