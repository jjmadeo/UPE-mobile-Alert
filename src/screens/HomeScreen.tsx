import React, { useEffect, useMemo } from 'react';
import {
  FlatList,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { useAuthStore } from '../state/authStore';
import { useAlertStore } from '../state/alertStore';
import { syncFcmTokenWithBackend } from '../notifications/fcm';
import { colors, radius, spacing, useTheme, Theme } from '../theme';

export function HomeScreen() {
  const theme = useTheme();
  const branding = useAuthStore(s => s.branding);
  const styles = useMemo(
    () => createStyles(theme, branding.primaryColor),
    [theme, branding.primaryColor],
  );
  const firefighter = useAuthStore(s => s.firefighter);
  const logout = useAuthStore(s => s.logout);
  const history = useAlertStore(s => s.history);

  useEffect(() => {
    // Por si el token rotó mientras la app estaba cerrada, o esta es la
    // primera vez que se abre ya logueado (sesión persistida).
    syncFcmTokenWithBackend();
  }, []);

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <View style={styles.institutionBadge} />
        <Text style={styles.institution}>{branding.institutionName}</Text>
        <Text style={styles.welcome}>Hola, {firefighter?.name ?? ''}</Text>
      </View>

      <View style={styles.statusCard}>
        <View style={styles.statusDot} />
        <Text style={styles.statusText}>Esperando alertas…</Text>
      </View>

      <Text style={styles.historyTitle}>Últimas respuestas</Text>
      <FlatList
        data={history}
        keyExtractor={item => `${item.alert.id}-${item.respondedAt}`}
        contentContainerStyle={styles.historyList}
        ListEmptyComponent={
          <Text style={styles.historyEmpty}>
            Todavía no respondiste ninguna alerta.
          </Text>
        }
        renderItem={({ item }) => (
          <View style={styles.historyItem}>
            <Text style={styles.historyAlertTitle}>{item.alert.title}</Text>
            <Text
              style={[
                styles.historyResponse,
                {
                  color:
                    item.response === 'ATTENDING'
                      ? theme.success
                      : theme.danger,
                },
              ]}
            >
              {item.response === 'ATTENDING' ? 'Asististe' : 'No asististe'}
            </Text>
            <Text style={styles.historyDate}>
              {new Date(item.respondedAt).toLocaleString()}
            </Text>
          </View>
        )}
      />

      <TouchableOpacity style={styles.logout} onPress={logout}>
        <Text style={styles.logoutLabel}>Cerrar sesión</Text>
      </TouchableOpacity>
    </View>
  );
}

/**
 * Antes esta pantalla pintaba TODO el fondo con `branding.primaryColor` (el
 * color de la institución) — quedaba plano y, para el azul de la
 * institución mock, poco prolijo. Ahora el fondo es neutro y sigue el tema
 * del sistema (`useTheme()`); el color de marca de la institución queda
 * como acento puntual — la barrita al lado del nombre y el punto de
 * estado — que es lo que realmente identifica al cuartel sin teñir toda la
 * pantalla.
 */
function createStyles(theme: Theme, brandColor: string) {
  return StyleSheet.create({
    container: {
      flex: 1,
      backgroundColor: theme.background,
      padding: spacing.xl,
      paddingTop: 56,
    },
    header: { marginBottom: spacing.xxl },
    institutionBadge: {
      width: 32,
      height: 4,
      borderRadius: radius.full,
      backgroundColor: brandColor,
      marginBottom: spacing.sm,
    },
    institution: { color: theme.textPrimary, fontSize: 22, fontWeight: '800' },
    welcome: { color: theme.textSecondary, fontSize: 15, marginTop: spacing.xs },
    statusCard: {
      backgroundColor: theme.surface,
      borderRadius: radius.xl,
      borderWidth: 1,
      borderColor: theme.border,
      padding: spacing.lg,
      flexDirection: 'row',
      alignItems: 'center',
      marginBottom: spacing.xxl,
    },
    statusDot: {
      width: 10,
      height: 10,
      borderRadius: radius.full,
      backgroundColor: colors.green500,
      marginRight: spacing.md - 2,
    },
    statusText: { color: theme.textPrimary, fontSize: 16, fontWeight: '600' },
    historyTitle: {
      color: theme.textPrimary,
      fontSize: 15,
      fontWeight: '700',
      marginBottom: spacing.sm,
    },
    historyList: { paddingBottom: spacing.lg },
    historyEmpty: { color: theme.textMuted },
    historyItem: {
      backgroundColor: theme.surface,
      borderRadius: radius.lg,
      borderWidth: 1,
      borderColor: theme.border,
      padding: spacing.md + 2,
      marginBottom: spacing.md - 2,
    },
    historyAlertTitle: {
      fontSize: 15,
      fontWeight: '700',
      color: theme.textPrimary,
    },
    historyResponse: { fontSize: 14, fontWeight: '700', marginTop: 2 },
    historyDate: { fontSize: 12, color: theme.textMuted, marginTop: spacing.xs },
    logout: { alignItems: 'center', paddingVertical: spacing.lg },
    logoutLabel: { color: theme.textSecondary, fontWeight: '600' },
  });
}
