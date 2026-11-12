package com.example.gearvrtest;

import android.app.Activity;
import android.content.ComponentName;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;

/**
 * Phone-launcher entry for the Gear VR app.
 *
 * Gear VR's Oculus Home never lists sideloaded apps, and starting a VR app from the
 * phone screen shown inside the headset runs it as a flat 2D window. Oculus Home's
 * shell (com.oculus.vrshell) accepts a LAUNCH broadcast from any app and then starts
 * the target properly in VR, exactly like a store app. This 2D activity sends that
 * broadcast for our own package and closes.
 *
 * If the shell doesn't take over (phone outside the headset, Home not running), it
 * falls back to starting the Unity activity directly (developer mode runs it in VR).
 */
public class VrLaunchActivity extends Activity {
    private boolean handedOver;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Intent launch = new Intent("com.oculus.vrshell.intent.action.LAUNCH");
        launch.setComponent(new ComponentName("com.oculus.vrshell",
                "com.oculus.vrshell.ShellControlBroadcastReceiver"));
        launch.putExtra("intent_data", "apk://" + getPackageName());
        sendBroadcast(launch);

        new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
            @Override
            public void run() {
                if (!handedOver) {
                    Intent app = new Intent();
                    app.setClassName(getPackageName(), "com.unity3d.player.UnityPlayerActivity");
                    app.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(app);
                }
                finish();
            }
        }, 2000);
    }

    @Override
    protected void onPause() {
        super.onPause();
        handedOver = true;   // the shell switched to VR and launched the app
    }
}
