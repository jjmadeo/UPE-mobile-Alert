import React, { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { AlertPayload, AlertResponseType } from '../types/alert';
import { BigButton } from '../components/BigButton';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { getCurrentLocation } from '../location/getCurrentLocation';
import { distanceKm, formatDistance } from '../location/distance';
import { respondToAlert } from '../api/alertsApi';
import { cancelAlertNotification } from '../notifications/displayAlertNotification';
import { useAlertStore } from '../state/alertStore';
import { colors, spacing } from '../theme';

interface AlertScreenProps {
  alert: AlertPayload;
}

const CONFIRM_COPY: Record<
  AlertResponseType,
  { title: string; message: string; color: string }
> = {
  ATTENDING: {
    title: 'Confirmar asistencia',
    message: '¿Confirmás que vas a asistir a este aviso?',
    color: colors.green600,
  },
  NOT_ATTENDING: {
    title: 'Confirmar que no asistís',
    message: '¿Confirmás que NO vas a asistir a este aviso?',
    color: colors.red700,
  },
};

/**
 * Pantalla que se muestra "por arriba" de toda la app (ver App.tsx) apenas
 * hay un aviso activo, sin importar en qué otra pantalla estuviera el
 * bombero. Roja siempre, a propósito: es la señal visual de "hay una
 * emergencia y tenés que responder", nunca cambia con el branding de la
 * institución.
 */
export function AlertScreen({ alert }: AlertScreenProps) {
  const recordResponse = useAlertStore(s => s.recordResponse);
  const [pendingResponse, setPendingResponse] =
    useState<AlertResponseType | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [distanceLabel, setDistanceLabel] = useState<string | null>(null);

  // Distancia al siniestro: se pide apenas se muestra la alerta (no cuando
  // el bombero confirma) para que ya esté calculada, sin retrasar en nada
  // el flujo de Asistir/No asistir. Si el backend no mandó coordenadas, o
  // no hay permiso/GPS a tiempo, simplemente no se muestra — nunca bloquea
  // ni rompe la pantalla (mismo criterio que getCurrentLocation en
  // handleConfirm).
  useEffect(() => {
    if (alert.latitude === undefined || alert.longitude === undefined) {
      return;
    }
    let cancelled = false;
    const alertLocation = { latitude: alert.latitude, longitude: alert.longitude };
    getCurrentLocation().then(deviceLocation => {
      if (cancelled || !deviceLocation) {
        return;
      }
      setDistanceLabel(formatDistance(distanceKm(deviceLocation, alertLocation)));
    });
    return () => {
      cancelled = true;
    };
  }, [alert.latitude, alert.longitude]);

  function handlePress(response: AlertResponseType) {
    setErrorMessage(null);
    setPendingResponse(response);
  }

  function handleCancel() {
    if (submitting) {
      return;
    }
    setPendingResponse(null);
    setErrorMessage(null);
  }

  async function handleConfirm() {
    if (!pendingResponse) {
      return;
    }
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const location = await getCurrentLocation();
      await respondToAlert(alert.id, pendingResponse, location);
      await cancelAlertNotification(alert.id);
      recordResponse(alert, pendingResponse);
    } catch (error) {
      console.warn('[AlertScreen] error al enviar respuesta', error);
      setErrorMessage(
        'No se pudo enviar tu respuesta. Revisá la conexión y volvé a intentar.',
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <View style={styles.container}>
      <View style={styles.content}>
        <Text style={styles.badge}>ALERTA</Text>
        <Text style={styles.title}>{alert.title}</Text>
        {alert.message ? (
          <Text style={styles.message}>{alert.message}</Text>
        ) : null}
        {alert.address ? (
          <Text style={styles.address}>
            📍 {alert.address}
            {distanceLabel ? ` · a ${distanceLabel}` : ''}
          </Text>
        ) : null}
        <Text style={styles.time}>
          {new Date(alert.createdAt).toLocaleTimeString()}
        </Text>
      </View>

      <View style={styles.actions}>
        <BigButton
          label="ASISTIR"
          backgroundColor={colors.white}
          textColor={colors.green600}
          onPress={() => handlePress('ATTENDING')}
          style={styles.buttonSpacing}
        />
        <BigButton
          label="NO ASISTIR"
          backgroundColor={colors.white}
          textColor={colors.red700}
          onPress={() => handlePress('NOT_ATTENDING')}
        />
      </View>

      <ConfirmDialog
        visible={pendingResponse !== null}
        title={pendingResponse ? CONFIRM_COPY[pendingResponse].title : ''}
        message={pendingResponse ? CONFIRM_COPY[pendingResponse].message : ''}
        confirmLabel="Confirmar"
        confirmColor={
          pendingResponse ? CONFIRM_COPY[pendingResponse].color : colors.gray900
        }
        submitting={submitting}
        errorMessage={errorMessage}
        onConfirm={handleConfirm}
        onCancel={handleCancel}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    ...StyleSheet.absoluteFill,
    backgroundColor: colors.red600,
    justifyContent: 'space-between',
    paddingHorizontal: spacing.xxl,
    paddingTop: 72,
    paddingBottom: spacing.xxxl + spacing.sm,
  },
  content: { alignItems: 'center' },
  badge: {
    color: colors.red100,
    fontSize: 14,
    fontWeight: '800',
    letterSpacing: 4,
    marginBottom: spacing.md,
  },
  title: {
    color: colors.white,
    fontSize: 30,
    fontWeight: '900',
    textAlign: 'center',
    marginBottom: spacing.md,
  },
  message: {
    color: colors.red50,
    fontSize: 18,
    textAlign: 'center',
    marginBottom: spacing.sm,
  },
  address: {
    color: colors.red50,
    fontSize: 16,
    textAlign: 'center',
    marginBottom: spacing.sm,
  },
  time: {
    color: colors.red300,
    fontSize: 14,
    marginTop: spacing.sm,
  },
  actions: { gap: spacing.lg },
  buttonSpacing: { marginBottom: 0 },
});
