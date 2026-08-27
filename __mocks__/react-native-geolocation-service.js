// Manual mock de Jest: la librería real registra un NativeEventEmitter al
// importarse, lo que revienta fuera de un runtime con módulos nativos.
module.exports = {
  requestAuthorization: jest.fn(async () => 'granted'),
  getCurrentPosition: jest.fn((success, _error, _options) => {
    success({
      coords: { latitude: 0, longitude: 0, accuracy: 5 },
      timestamp: Date.now(),
    });
  }),
  watchPosition: jest.fn(() => 1),
  clearWatch: jest.fn(),
  stopObserving: jest.fn(),
};
