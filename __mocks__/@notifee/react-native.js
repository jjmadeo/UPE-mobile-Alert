// Manual mock de Jest: notifee ships su propio mock oficial para tests, acá
// solo lo re-exponemos para que se use automáticamente en toda la suite.
// https://notifee.app/react-native/docs/testing
module.exports = require('@notifee/react-native/jest-mock');
