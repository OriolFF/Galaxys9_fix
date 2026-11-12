package com.gearvrfix.guard;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

/** Boot, app update, or a manual "com.gearvrfix.guard.CHECK" broadcast. */
public class StartReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context ctx, Intent intent) {
        Guard.check(ctx, String.valueOf(intent.getAction()));
        Guard.schedule(ctx);
    }
}
