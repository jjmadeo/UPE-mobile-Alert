import React, { useMemo } from 'react';
import {
  ActivityIndicator,
  Modal,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';
import { colors, radius, spacing, useTheme, Theme } from '../theme';

interface ConfirmDialogProps {
  visible: boolean;
  title: string;
  message: string;
  confirmLabel: string;
  confirmColor: string;
  submitting: boolean;
  errorMessage?: string | null;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Paso de confirmación obligatorio antes de mandar la respuesta al backend.
 * Existe puntualmente para evitar que un toque accidental sobre "Asistir" /
 * "No asistir" (pantalla grande, contexto de apuro) se registre sin que el
 * bombero lo haya querido.
 */
export function ConfirmDialog({
  visible,
  title,
  message,
  confirmLabel,
  confirmColor,
  submitting,
  errorMessage,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const theme = useTheme();
  const styles = useMemo(() => createStyles(theme), [theme]);

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onCancel}
    >
      <View style={styles.backdrop}>
        <View style={styles.card}>
          <Text style={styles.title}>{title}</Text>
          <Text style={styles.message}>{message}</Text>

          {errorMessage ? (
            <Text style={styles.error}>{errorMessage}</Text>
          ) : null}

          <TouchableOpacity
            style={[styles.confirmButton, { backgroundColor: confirmColor }]}
            onPress={onConfirm}
            disabled={submitting}
          >
            {submitting ? (
              <ActivityIndicator color={colors.white} />
            ) : (
              <Text style={styles.confirmLabel}>{confirmLabel}</Text>
            )}
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.cancelButton}
            onPress={onCancel}
            disabled={submitting}
          >
            <Text style={styles.cancelLabel}>Cancelar</Text>
          </TouchableOpacity>
        </View>
      </View>
    </Modal>
  );
}

function createStyles(theme: Theme) {
  return StyleSheet.create({
    backdrop: {
      flex: 1,
      backgroundColor: theme.overlay,
      alignItems: 'center',
      justifyContent: 'center',
      padding: spacing.xxl,
    },
    card: {
      width: '100%',
      maxWidth: 420,
      backgroundColor: theme.surface,
      borderRadius: radius.xxxl,
      padding: spacing.xxl,
    },
    title: {
      fontSize: 20,
      fontWeight: '800',
      color: theme.textPrimary,
      marginBottom: spacing.sm,
      textAlign: 'center',
    },
    message: {
      fontSize: 16,
      color: theme.textSecondary,
      textAlign: 'center',
      marginBottom: spacing.xl,
    },
    error: {
      color: theme.danger,
      textAlign: 'center',
      marginBottom: spacing.md,
    },
    confirmButton: {
      minHeight: 56,
      borderRadius: radius.xl,
      alignItems: 'center',
      justifyContent: 'center',
      marginBottom: spacing.md,
    },
    confirmLabel: {
      color: colors.white,
      fontSize: 18,
      fontWeight: '700',
    },
    cancelButton: {
      minHeight: 48,
      alignItems: 'center',
      justifyContent: 'center',
    },
    cancelLabel: {
      color: theme.textMuted,
      fontSize: 16,
      fontWeight: '600',
    },
  });
}
