import React, { useEffect } from 'react';
import { StatusBar, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { RootNavigator } from './src/navigation/RootNavigator';
import { AlertScreen } from './src/screens/AlertScreen';
import { useAlertStore } from './src/state/alertStore';
import {
  checkInitialNotification,
  registerNotifeeBackgroundHandler,
  subscribeToForegroundMessages,
  subscribeToNotifeeForegroundEvents,
  subscribeToTokenRefresh,
} from './src/notifications/fcm';

export default function App() {
  const currentAlert = useAlertStore(s => s.currentAlert);

  useEffect(() => {
    registerNotifeeBackgroundHandler();
    checkInitialNotification();

    const unsubForeground = subscribeToForegroundMessages();
    const unsubNotifeeEvents = subscribeToNotifeeForegroundEvents();
    const unsubTokenRefresh = subscribeToTokenRefresh();

    return () => {
      unsubForeground();
      unsubNotifeeEvents();
      unsubTokenRefresh();
    };
  }, []);

  return (
    <SafeAreaProvider>
      <StatusBar barStyle="light-content" />
      <View style={{ flex: 1 }}>
        <NavigationContainer>
          <RootNavigator />
        </NavigationContainer>
        {/* Overlay: se pinta encima de lo que sea que esté abierto en el
            stack apenas hay un aviso activo. Ver comentario en
            RootNavigator.tsx sobre por qué esto no es una ruta más. */}
        {currentAlert ? (
          // key={currentAlert.id}: fuerza un remount completo por cada
          // aviso nuevo. Sin esto, si llega una alerta nueva mientras
          // AlertScreen sigue con el ConfirmDialog de una anterior abierto
          // (p.ej. trabado esperando la ubicación, ver getCurrentLocation),
          // React reutiliza la misma instancia y ese estado colgado
          // (submitting/pendingResponse) tapa la alerta nueva en vez de
          // mostrarla limpia.
          <AlertScreen key={currentAlert.id} alert={currentAlert} />
        ) : null}
      </View>
    </SafeAreaProvider>
  );
}
