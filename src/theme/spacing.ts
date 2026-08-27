/**
 * Escala de espaciado, calcada de la escala de Tailwind (1 unidad = 4px:
 * spacing.md = "space-3", spacing.xl = "space-5", etc.) para no inventar
 * números sueltos por archivo.
 */
export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
} as const;

/** Escala de radios, ídem — nombres tipo "rounded-lg/xl/2xl" de Tailwind. */
export const radius = {
  md: 10,
  lg: 12,
  xl: 14,
  xxl: 16,
  xxxl: 20,
  full: 999,
} as const;
