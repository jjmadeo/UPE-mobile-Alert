import React from 'react';
import {
  ActivityIndicator,
  StyleSheet,
  Text,
  TouchableOpacity,
  ViewStyle,
} from 'react-native';
import { colors, radius } from '../theme';

interface BigButtonProps {
  label: string;
  onPress: () => void;
  backgroundColor: string;
  textColor?: string;
  disabled?: boolean;
  loading?: boolean;
  style?: ViewStyle;
}

/**
 * Botón grande a propósito: la app la usan bomberos, muchas veces con
 * guantes puestos o mirando de reojo el teléfono. El hit-area chica es
 * justamente lo que el pedido original pide evitar ("errores de touch").
 */
export function BigButton({
  label,
  onPress,
  backgroundColor,
  textColor = colors.gray900,
  disabled,
  loading,
  style,
}: BigButtonProps) {
  return (
    <TouchableOpacity
      accessibilityRole="button"
      accessibilityLabel={label}
      activeOpacity={0.75}
      disabled={disabled || loading}
      onPress={onPress}
      style={[
        styles.button,
        { backgroundColor, opacity: disabled ? 0.6 : 1 },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={textColor} />
      ) : (
        <Text style={[styles.label, { color: textColor }]}>{label}</Text>
      )}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  button: {
    minHeight: 96,
    borderRadius: radius.xxl,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 16,
  },
  label: {
    fontSize: 26,
    fontWeight: '800',
    letterSpacing: 0.5,
  },
});
