module.exports = {
  preset: '@react-native/jest-preset',
  transformIgnorePatterns: [
    'node_modules/(?!(' +
      [
        '@react-native',
        'react-native',
        '@react-navigation',
        '@react-native-firebase',
        '@notifee/react-native',
        'react-native-safe-area-context',
        'react-native-screens',
        'react-native-geolocation-service',
        '@react-native-async-storage/async-storage',
      ].join('|') +
      ')/)',
  ],
};
