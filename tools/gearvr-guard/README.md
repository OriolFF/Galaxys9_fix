# Gear VR Guard

A tiny Android app (no screen, about 12 KB) that keeps `Settings.Global vr_setupwizard_completed` at `1`.

Gear VR's setup wizard, Gear VR Service and Meta's updaters set that value to `0` or `2` whenever an online update check fails. Samsung's and Meta's servers are shut down, so those checks always fail, and the headset gets stuck in the "remove the device and update" loop. The guard writes `1` back immediately. It runs:
- whenever the value changes (a JobScheduler content-URI trigger),
- every 15 minutes as a backstop,
- at boot.

It does nothing else: no network, no other settings. You can see what it does with `adb logcat -s GearVrGuard`.

## Build

```bash
bash tools/gearvr-guard/build.sh
```

This needs Android SDK build-tools 36.1.0, platform android-34, and Android Studio's JBR. The signing key is created in `%USERPROFILE%\.android\gearvr-guard.keystore`, outside the repo.

## Install (once, phone on USB)

```bash
adb install -r tools/gearvr-guard/out/gearvr-guard.apk
adb shell pm grant com.gearvrfix.guard android.permission.WRITE_SECURE_SETTINGS
adb shell dumpsys deviceidle whitelist +com.gearvrfix.guard
adb shell am broadcast -a com.gearvrfix.guard.CHECK -n com.gearvrfix.guard/.StartReceiver -f 0x20
```

The last line starts the guard for the first time. A freshly installed app stays inactive until something starts it. After that it restarts itself at every boot.

## Remove

```bash
adb uninstall com.gearvrfix.guard
```
