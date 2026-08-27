// Manual mock de Jest para @react-native-firebase/messaging (API modular
// v26+). react-native-firebase no distribuye un mock oficial para esta API
// nueva, así que se stubea acá lo mínimo que usa src/notifications/fcm.ts.
module.exports = {
  getMessaging: jest.fn(() => ({})),
  getToken: jest.fn(async () => 'mock-fcm-token'),
  onMessage: jest.fn(() => jest.fn()),
  onTokenRefresh: jest.fn(() => jest.fn()),
  setBackgroundMessageHandler: jest.fn(),
};
