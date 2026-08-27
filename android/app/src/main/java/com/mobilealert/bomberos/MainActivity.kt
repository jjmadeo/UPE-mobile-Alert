package com.mobilealert.bomberos

import android.os.Build
import android.os.Bundle
import android.view.WindowManager
import com.facebook.react.ReactActivity
import com.facebook.react.ReactActivityDelegate
import com.facebook.react.defaults.DefaultNewArchitectureEntryPoint.fabricEnabled
import com.facebook.react.defaults.DefaultReactActivityDelegate

class MainActivity : ReactActivity() {

  /**
   * Returns the name of the main component registered from JavaScript. This is used to schedule
   * rendering of the component.
   */
  override fun getMainComponentName(): String = "MobileAlert"

  /**
   * Returns the instance of the [ReactActivityDelegate]. We use [DefaultReactActivityDelegate]
   * which allows you to enable New Architecture with a single boolean flags [fabricEnabled]
   */
  override fun createReactActivityDelegate(): ReactActivityDelegate =
      DefaultReactActivityDelegate(this, mainComponentName, fabricEnabled)

  /**
   * Necesario para que el `fullScreenAction` de las notificaciones de alerta
   * (ver src/notifications/displayAlertNotification.ts) realmente se muestre
   * sobre la pantalla de bloqueo y prenda la pantalla, en lugar de quedar
   * detrás del lock screen esperando que el usuario desbloquee el teléfono.
   */
  override fun onCreate(savedInstanceState: Bundle?) {
    // Se pasa `null` a propósito, ignorando el savedInstanceState real: es
    // el fix documentado de react-native-screens para un crash de cold
    // start bien conocido (Fragment$InstantiationException en
    // ScreenStackFragment) que se dispara justo en el escenario que más nos
    // importa acá — el proceso murió y Android reconstruye la Activity
    // (p.ej. por el fullScreenAction de una alerta) intentando restaurar el
    // estado de navegación de un Fragment antes de que React Native esté
    // listo para reconstruirlo. React Native maneja su propio estado de
    // navegación desde JS, así que no hay nada útil que restaurar acá — ver
    // https://github.com/software-mansion/react-native-screens#android.
    super.onCreate(null)
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
      setShowWhenLocked(true)
      setTurnScreenOn(true)
    } else {
      @Suppress("DEPRECATION")
      window.addFlags(
          WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
              WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON or
              WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON,
      )
    }
  }
}
