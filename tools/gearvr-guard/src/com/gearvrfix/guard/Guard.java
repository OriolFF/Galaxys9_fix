package com.gearvrfix.guard;

import android.app.job.JobInfo;
import android.app.job.JobScheduler;
import android.content.ComponentName;
import android.content.ContentResolver;
import android.content.Context;
import android.provider.Settings;
import android.util.Log;

/**
 * Keeps Settings.Global "vr_setupwizard_completed" at 1 (Gear VR setup completed).
 *
 * Several parts of the Gear VR software (the setup wizard's update job, Gear VR
 * Service's dock check, Meta app updates) reset it to 0/2 after failing to reach
 * the dead Samsung/Meta servers, which traps the headset in the
 * "remove the device and update" loop. This puts it straight back.
 *
 * Triggers: a JobScheduler content-URI job fires whenever the setting changes,
 * a periodic job every 15 minutes as a backstop, plus boot / app update.
 */
final class Guard {
    static final String TAG = "GearVrGuard";
    static final String KEY = "vr_setupwizard_completed";
    static final int JOB_ON_CHANGE = 1;
    static final int JOB_PERIODIC = 2;

    static void check(Context ctx, String why) {
        ContentResolver cr = ctx.getContentResolver();
        int v = Settings.Global.getInt(cr, KEY, -1);
        if (v != 1) {
            try {
                Settings.Global.putInt(cr, KEY, 1);
                Log.w(TAG, why + ": " + KEY + " was " + v + ", reset to 1");
            } catch (SecurityException e) {
                Log.e(TAG, "Missing WRITE_SECURE_SETTINGS. Run: adb shell pm grant "
                        + ctx.getPackageName() + " android.permission.WRITE_SECURE_SETTINGS", e);
            }
        } else {
            Log.i(TAG, why + ": " + KEY + "=1 OK");
        }
    }

    static void schedule(Context ctx) {
        JobScheduler js = ctx.getSystemService(JobScheduler.class);
        ComponentName svc = new ComponentName(ctx, GuardJobService.class);

        // Fires as soon as the setting changes (content-URI jobs are one-shot; the
        // service reschedules it each time it runs).
        JobInfo onChange = new JobInfo.Builder(JOB_ON_CHANGE, svc)
                .addTriggerContentUri(new JobInfo.TriggerContentUri(
                        Settings.Global.getUriFor(KEY), 0))
                .setTriggerContentUpdateDelay(0)
                .setTriggerContentMaxDelay(1000)
                .build();
        js.schedule(onChange);

        // Backstop, survives reboots.
        if (js.getPendingJob(JOB_PERIODIC) == null) {
            JobInfo periodic = new JobInfo.Builder(JOB_PERIODIC, svc)
                    .setPeriodic(15 * 60 * 1000L)
                    .setPersisted(true)
                    .build();
            js.schedule(periodic);
        }
    }

    private Guard() {}
}
