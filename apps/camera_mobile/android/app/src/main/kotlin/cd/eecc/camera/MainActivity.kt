package cd.eecc.camera

import android.graphics.ImageFormat
import android.graphics.Rect
import android.graphics.YuvImage
import android.view.WindowManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.ByteArrayOutputStream

class MainActivity: FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "cd.eecc.camera/device")
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "keepAwake" -> {
                        val on = call.argument<Boolean>("on") == true
                        runOnUiThread {
                            if (on) window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
                            else window.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
                        }
                        result.success(true)
                    }
                    "jpeg" -> {
                        val nv21 = call.argument<ByteArray>("nv21")
                        val width = call.argument<Int>("width") ?: 0
                        val height = call.argument<Int>("height") ?: 0
                        if (nv21 == null || width <= 0 || height <= 0) {
                            result.success(null)
                            return@setMethodCallHandler
                        }
                        try {
                            val image = YuvImage(nv21, ImageFormat.NV21, width, height, null)
                            val out = ByteArrayOutputStream()
                            image.compressToJpeg(Rect(0, 0, width, height), 92, out)
                            result.success(out.toByteArray())
                        } catch (_: Exception) {
                            result.success(null)
                        }
                    }
                    else -> result.notImplemented()
                }
            }
    }
}
