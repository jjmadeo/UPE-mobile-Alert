import React, { useMemo, useState } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  StyleSheet,
  Text,
  TextInput,
  TouchableOpacity,
  View,
} from 'react-native';
import { login } from '../api/authApi';
import { useAuthStore } from '../state/authStore';
import {
  requestNotificationPermission,
  syncFcmTokenWithBackend,
} from '../notifications/fcm';
import { colors, radius, spacing, useTheme, Theme } from '../theme';

export function LoginScreen() {
  const theme = useTheme();
  const styles = useMemo(() => createStyles(theme), [theme]);
  const setSession = useAuthStore(s => s.setSession);
  const [institutionCode, setInstitutionCode] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSubmit =
    institutionCode.trim().length > 0 &&
    username.trim().length > 0 &&
    password.length > 0 &&
    !loading;

  async function handleSubmit() {
    setError(null);
    setLoading(true);
    try {
      const result = await login({
        institutionCode: institutionCode.trim(),
        username: username.trim(),
        password,
      });
      setSession(result);
      await requestNotificationPermission();
      await syncFcmTokenWithBackend();
    } catch (err: any) {
      setError(
        err?.response?.data?.message ??
          'No se pudo iniciar sesión. Verificá los datos y tu conexión.',
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <Text style={styles.title}>Mobile Alert</Text>
      <Text style={styles.subtitle}>Alertas para bomberos</Text>

      <View style={styles.form}>
        <Text style={styles.label}>Código de institución</Text>
        <TextInput
          style={styles.input}
          value={institutionCode}
          onChangeText={setInstitutionCode}
          placeholder="ej: BOMBEROS-CENTRAL"
          placeholderTextColor={theme.textMuted}
          autoCapitalize="characters"
          autoCorrect={false}
        />

        <Text style={styles.label}>Usuario</Text>
        <TextInput
          style={styles.input}
          value={username}
          onChangeText={setUsername}
          placeholder="usuario"
          placeholderTextColor={theme.textMuted}
          autoCapitalize="none"
          autoCorrect={false}
        />

        <Text style={styles.label}>Contraseña</Text>
        <TextInput
          style={styles.input}
          value={password}
          onChangeText={setPassword}
          placeholder="••••••••"
          placeholderTextColor={theme.textMuted}
          secureTextEntry
        />

        {error ? <Text style={styles.error}>{error}</Text> : null}

        <TouchableOpacity
          style={[styles.submit, { opacity: canSubmit ? 1 : 0.5 }]}
          onPress={handleSubmit}
          disabled={!canSubmit}
        >
          {loading ? (
            <ActivityIndicator color={colors.white} />
          ) : (
            <Text style={styles.submitLabel}>Ingresar</Text>
          )}
        </TouchableOpacity>
      </View>
    </KeyboardAvoidingView>
  );
}

/**
 * Estilos como función del tema en vez de `StyleSheet.create` estático: acá
 * es donde se resuelve claro/oscuro (`useTheme()` sigue el tema del
 * sistema). El resto de los tonos "fijos" (blanco del botón, texto sobre el
 * acento) siguen viniendo de `colors`, que no cambia con el modo.
 */
function createStyles(theme: Theme) {
  return StyleSheet.create({
    container: {
      flex: 1,
      backgroundColor: theme.background,
      justifyContent: 'center',
      padding: spacing.xxl,
    },
    title: {
      fontSize: 32,
      fontWeight: '800',
      color: theme.textPrimary,
      textAlign: 'center',
    },
    subtitle: {
      fontSize: 15,
      color: theme.textSecondary,
      textAlign: 'center',
      marginBottom: spacing.xxxl,
    },
    form: {
      backgroundColor: theme.surface,
      borderRadius: radius.xxxl,
      borderWidth: 1,
      borderColor: theme.border,
      padding: spacing.xxl,
    },
    label: {
      fontSize: 13,
      fontWeight: '600',
      color: theme.textSecondary,
      marginBottom: spacing.xs + 2,
      marginTop: spacing.md,
    },
    input: {
      borderWidth: 1,
      borderColor: theme.border,
      borderRadius: radius.md,
      paddingHorizontal: spacing.md + 2,
      paddingVertical: spacing.md,
      fontSize: 16,
      color: theme.textPrimary,
      backgroundColor: theme.background,
    },
    error: {
      color: theme.danger,
      marginTop: spacing.md + 2,
      textAlign: 'center',
    },
    submit: {
      marginTop: spacing.xxl,
      backgroundColor: theme.accent,
      borderRadius: radius.lg,
      minHeight: 52,
      alignItems: 'center',
      justifyContent: 'center',
    },
    submitLabel: {
      color: colors.white,
      fontSize: 17,
      fontWeight: '700',
    },
  });
}
