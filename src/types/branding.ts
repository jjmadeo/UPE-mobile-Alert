/**
 * Configuración white-label que devuelve el backend en el login, en base al
 * código de institución que ingresa el bombero. Permite que la misma app
 * (mismo APK) se vea distinta para cada cuartel/institución sin recompilar.
 */
export interface BrandingConfig {
  institutionCode: string;
  institutionName: string;
  /** Color principal de la app (se usa en botones, header, etc). No se usa
   * para la pantalla de alerta, que siempre es roja a propósito. */
  primaryColor: string;
  logoUrl?: string;
  /** Base URL del backend real de esta institución. En el mock siempre
   * apunta al mismo mock-server, pero en producción cada institución podría
   * tener su propio backend. */
  backendUrl: string;
}

export const DEFAULT_BRANDING: BrandingConfig = {
  institutionCode: '',
  institutionName: 'Mobile Alert',
  primaryColor: '#1E3A8A',
  backendUrl: '',
};
