/**
 * @format
 */

import notifee from '@notifee/react-native';
import { AppRegistry } from 'react-native';
import App from './App';
import { name as appName } from './app.json';
import { registerBackgroundMessageHandler } from './src/notifications/fcm';
import { ensureAlertChannel } from './src/notifications/channel';

// Tiene que registrarse acá, a nivel de módulo, ANTES de
// AppRegistry.registerComponent. Si se registra dentro de un componente
// (ej: en un useEffect de App.tsx) no se llega a ejecutar cuando la app está
// completamente cerrada, que es justamente el caso más importante para esta
// app (recibir la alerta con el proceso muerto).
registerBackgroundMessageHandler();

// Antes solo se creaba al mostrar la primera alerta (dentro de
// displayAlertNotification). Eso no alcanza para el push de respaldo (ver
// FcmSender.SendFallbackNotificationAsync): ese lo muestra Android solo,
// sin correr código nuestro, así que si el canal todavía no existe la
// primera vez que llega una alerta, sale con sonido/vibración default en
// vez de los nuestros. Creándolo acá, a nivel de módulo, ya existe desde el
// primer arranque de la app, sea cual sea el motivo por el que arrancó.
ensureAlertChannel();

// Requisito de notifee para poder mostrar la alerta con
// `asForegroundService: true` (ver displayAlertNotification.ts) — sin este
// registro previo, displayNotification tira error al pedir el foreground
// service. La promesa se queda sin resolver a propósito: el service lo
// para explícitamente cancelAlertNotification al responder, o si no
// Android lo corta solo por ser tipo shortService.
notifee.registerForegroundService(() => new Promise(() => {}));

AppRegistry.registerComponent(appName, () => App);
