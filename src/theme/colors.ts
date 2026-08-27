/**
 * Paleta de la app. Son los valores default de Tailwind CSS
 * (https://tailwindcss.com/docs/colors) en hex plano — no hay NativeWind ni
 * ningún motor de utility classes instalado, así que esto es simplemente
 * una única fuente de verdad con nombres consistentes, en vez de los
 * mismos hex sueltos repetidos (y a veces ligeramente distintos) en cada
 * pantalla. Agregar un tono acá antes que hardcodear un hex nuevo en un
 * componente.
 *
 * Estos son los tonos CRUDOS (fijos, no cambian con claro/oscuro) — para
 * fondos/textos que sí tienen que adaptarse al tema del sistema, ver
 * `./semanticTheme.ts` (`useTheme()`).
 */
export const colors = {
  white: '#FFFFFF',

  slate50: '#F8FAFC',
  slate100: '#F1F5F9',
  slate200: '#E2E8F0',
  slate300: '#CBD5E1',
  slate400: '#94A3B8',
  slate500: '#64748B',
  slate600: '#475569',
  slate700: '#334155',
  slate800: '#1E293B',
  slate900: '#0F172A',
  slate950: '#020617',

  gray200: '#E5E7EB',
  gray300: '#D1D5DB',
  gray500: '#6B7280',
  gray700: '#374151',
  gray900: '#111827',

  blue900: '#1E3A8A',

  indigo500: '#6366F1',
  indigo600: '#4F46E5',

  green500: '#22C55E',
  green600: '#16A34A',

  red50: '#FEF2F2',
  red100: '#FEE2E2',
  red300: '#FCA5A5',
  red400: '#F87171',
  red600: '#DC2626',
  red700: '#B91C1C',

  overlayLight: 'rgba(15,23,42,0.55)',
  overlayDark: 'rgba(0,0,0,0.7)',
  whiteWash15: 'rgba(255,255,255,0.15)',
} as const;
