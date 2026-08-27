import { useColorScheme } from 'react-native';
import { colors } from './colors';

/**
 * Tokens semánticos que SÍ cambian entre claro y oscuro (a diferencia de
 * `colors`, que son tonos fijos). Se usan para fondo/superficie/texto de
 * las pantallas "normales" (Login, Home). La pantalla de alerta
 * (AlertScreen) queda afuera de este sistema a propósito: es siempre roja,
 * no debe cambiar con el tema — es la señal de emergencia, ver el
 * comentario en AlertScreen.tsx.
 */
export interface Theme {
  mode: 'light' | 'dark';
  /** Fondo de la pantalla. */
  background: string;
  /** Fondo de tarjetas/superficies elevadas sobre `background`. */
  surface: string;
  /** Variante sutil de `surface`, para pills/badges sin tanto contraste. */
  surfaceAlt: string;
  /** Bordes finos — reemplazan a las sombras para un look más plano. */
  border: string;
  textPrimary: string;
  textSecondary: string;
  textMuted: string;
  /** Color de acción por defecto (botones primarios) cuando no hay branding
   * de institución de por medio, ej. el botón "Ingresar" del login. */
  accent: string;
  success: string;
  danger: string;
  /** Fondo semitransparente para modales (ConfirmDialog). */
  overlay: string;
}

const lightTheme: Theme = {
  mode: 'light',
  background: colors.slate50,
  surface: colors.white,
  surfaceAlt: colors.slate100,
  border: colors.slate200,
  textPrimary: colors.slate900,
  textSecondary: colors.slate600,
  textMuted: colors.slate400,
  accent: colors.indigo600,
  success: colors.green600,
  danger: colors.red700,
  overlay: colors.overlayLight,
};

const darkTheme: Theme = {
  mode: 'dark',
  background: colors.slate950,
  surface: colors.slate800,
  surfaceAlt: colors.slate700,
  border: colors.slate700,
  textPrimary: colors.white,
  textSecondary: colors.slate300,
  textMuted: colors.slate500,
  accent: colors.indigo500,
  success: colors.green500,
  // red700 pierde legibilidad sobre superficies oscuras; red400 mantiene
  // suficiente contraste sin perder el significado "peligro/no asistir".
  danger: colors.red400,
  overlay: colors.overlayDark,
};

/** Sigue el tema del sistema operativo (claro/oscuro) automáticamente. */
export function useTheme(): Theme {
  const scheme = useColorScheme();
  return scheme === 'dark' ? darkTheme : lightTheme;
}
