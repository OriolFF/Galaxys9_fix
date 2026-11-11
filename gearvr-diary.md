# Gear VR on Galaxy S9: repair diary

Phone: Samsung Galaxy S9 (SM-G960F, starltexx), Android 10, CSC PHE (Spain). (ADB serial omitted.)

## 2026-11-09: diagnosis

**Symptom.** Docking the phone opens the VR setup wizard ("Descargar aplicaciones VR"). It lists 7 Oculus components at 0,0MB. Tapping "Actualizar" ends in "Se ha producido un error de red" and the screen repeats forever. An earlier attempt also hit a Facebook login loop.

**Installed versions (adb dumpsys):**
| Package | Version |
|---|---|
| com.oculus.horizon (Oculus) | 9.0.0.681.473 |
| com.oculus.home | 9.0.0.257.473 |
| com.oculus.vrshell | 9.0.0.238.1458 |
| com.oculus.systemdriver | 9.0.0.232.450 |
| com.oculus.systemactivities | 9.0.0.232.450 |
| com.oculus.ocms | 9.0.0.257.473 |
| com.samsung.android.hmt.vrsvc | 3.2.04.10 |
| com.samsung.android.hmt.vrsystem | 3.2.03.3 |
| com.samsung.android.app.vrsetupwizard | 3.2.08.2 |
| com.facebook.katana | 513.1.0.66.92 |

**Root cause (from logcat of com.samsung.android.app.vrsetupwizard):**
1. The Samsung package check against vas.samsungapps.com succeeds (HTTP 200).
2. The wizard fetches `https://www.oculus.com/gear-vr/oculus_initial_app.xml`, which also returns HTTP 200.
3. That manifest now lists only 3 packages: com.facebook.appmanager, com.oculus.browser and com.oculus.mediaplayer, with fbcdn links.
4. The 7 core components are missing from it, so the wizard has an empty download URL for each: `downloading : Oculus System Driver/OculusSystemDriver` followed by `downloading : ` (blank), then `EVENT_ERROR_OCCURRED`, `network_error`.

Conclusion: tapping Actualizar can never succeed. The core APKs must be supplied by hand.

**Research:** gearvr.net "unbrick" guide (2024). Install the archived core APKs from vault.gearvr.net one by one while the wizard is on its error screen, then firewall the wizard and Oculus App Runtime with NetGuard.

## Attempts

### 18:07: what the archive offers
vault.gearvr.net's `Oculus_Install_Files.zip` contains the **same 9.0.0.x builds already on this phone**. The wizard log shows it listening for `onReceiver:PackageAdded()`. Idea: the wizard only needs a PACKAGE_ADDED event per component, not a newer version. So I can reinstall the phone's own copies, with no third-party downloads.

### 18:08: pull the APKs from the phone
`adb shell pm path <pkg>` then `adb pull` for systemdriver, systemactivities, horizon, vrshell, vrshell.home, home, ocms, twilight. (Pitfall: Git Bash rewrote `/data/...` into `C:/Program Files/Git/data/...`; fixed with `MSYS_NO_PATHCONV=1`.)

### 18:09–18:10: reinstall with `adb install -r` while the wizard shows its network error
| Wizard item | Package | Result |
|---|---|---|
| Oculus System Driver | com.oculus.systemdriver | Success, turned blue |
| Oculus System Activities | com.oculus.systemactivities | Success, turned blue |
| Oculus | com.oculus.horizon | Success, turned blue |
| Oculus VR | com.oculus.vrshell.home | Success, turned blue |
| Oculus Home | com.oculus.home | Success, turned blue |
| Oculus App Runtime | com.oculus.vrshell | Success, turned blue (UI updates a few seconds late) |
| Oculus Core Mobile Services | com.oculus.ocms | **Blocked by Google Play Protect** ("Aplicación dañina bloqueada") |

The existing OCMS install was untouched by the blocked attempt, so nothing is lost.

### 18:12: blocker
Reinstalling OCMS needs Play Protect's ADB-install scan turned off temporarily. I tried `settings put global verifier_verify_adb_installs 0`, but Claude Code's safety classifier refused it because it weakens a security setting. **Waiting for the user's decision.**

### 18:13: workarounds that leave security settings alone
- The wizard task survived the Play Protect dialog. `am start -n com.samsung.android.app.vrsetupwizard/.ui.LaunchActivity` brings it back with all 6 blue ticks kept. (The inner DownloadInstallActivity isn't exported, so it can't be launched directly.)
- `cmd package install-existing com.oculus.ocms` reports "installed for user: 0" but sends no PACKAGE_ADDED. The wizard did not react, so OCMS is still grey.
- Conclusion: OCMS needs a real reinstall, which Play Protect blocks. The user must either turn off "Analizar aplicaciones con Play Protect" for a few minutes or authorize me to do it over ADB.

### 18:14: OCMS installed (user authorized the Play Protect toggle)
`settings put global verifier_verify_adb_installs 0`, then `adb install -r ocms`, then set back to `1`. All 7 items turned blue, **but the wizard did not move on**. Tapping "Actualizar" was a mistake: it re-ran the online check and reset everything to grey.

### 18:20: new symptom, "Oculus sigue sin funcionar"
With Wi-Fi off, the wizard's COMENZAR showed an Oculus crash dialog. Crash report (`dumpsys dropbox data_app_crash`):
```
com.oculus.horizon: SecurityException: Access denied. Caller is not trusted
  at com.facebook.oxygen.preloads.sdk.firstparty.settings.FirstPartySettings.querySettings
```
Cause: **the Play Store auto-updated Meta App Manager (com.facebook.appmanager) at 18:14, from system 55.1.0 to 137.0.26.** The new version no longer trusts the old Oculus app's signature.
Fix: `pm uninstall com.facebook.appmanager`, which removes the updates and restores the factory 55.1.0. After that, the Oculus app opens normally and shows its Store with **no Facebook login loop**.

### 11-10 09:18: wizard needs a network
Offline, the VR service stub says "No hay conexión de red". Online, it goes to the "Descargar aplicaciones VR" list again. I re-ran the 7 reinstalls: Actualizar, then wait for network_error, then the 6 installs, then OCMS with the scan briefly off. All blue again, still no completion. The wizard simply never finishes, because its download step has no URLs.

### 09:23: found the real gate, `vr_setupwizard_completed`
`settings list global` shows `vr_setupwizard_completed=2`. Disassembled com.samsung.android.hmt.vrsvc with build-tools `dexdump`:
- `uo.vqCz()` reads `Settings.Global vr_setupwizard_completed % 3`.
- Log strings: `"setupWizardComplete=2! not completed!"`, `"setupWizardComplete=0! not completed!"`, `"set vr_setupwizard_completed=1 sideload="`, `"set vr_setupwizard_completed=1 demoMode="`.
- So **1 = completed**, and 0 or 2 = not completed. The service itself writes 1 in its sideload and demo modes. It resets the flag to 0 if the OS was upgraded, a default package is missing, or the System Driver or VR System is incompatible.

Fix: `adb shell settings put global vr_setupwizard_completed 1`.
Result: LaunchActivity now opens the **Samsung Gear VR launcher** (VRLauncherActivity) listing the installed VR apps (Galería, Samsung VR, SkiJumping, Bait!, Disney Movies VR, YouTube VR, Minecraft, Space Explorer, Smash Hit VR, Prime Video VR…), instead of the update loop.

### State at 09:24
- Play Protect ADB scan: restored (`verifier_verify_adb_installs=1`).
- com.facebook.appmanager: 55.1.0 (factory). **Watch out: the Play Store may auto-update it again.**
- vr_setupwizard_completed: 1
- Still to do: a real test in the headset, and preventing App Manager from auto-updating.

### 09:25: ✅ headset test passed
The user unplugged USB, docked the S9 in the Gear VR and confirmed **Oculus Home loads in VR**. The log capture ended at the unplug, so the dock itself isn't in it.

## Summary: what was actually wrong
1. **The wizard loops forever.** Meta's manifest (`oculus.com/gear-vr/oculus_initial_app.xml`) no longer lists the 7 core Oculus packages, so "Actualizar" has nothing to download and always fails. The wizard never writes `vr_setupwizard_completed=1`, so every dock goes back to it.
2. **Oculus crashes** ("Oculus sigue sin funcionar"). The Play Store auto-updated *Meta App Manager*, and the new version rejects the old Oculus app as an untrusted caller.

## The fix (for next time)
```
adb shell pm uninstall com.facebook.appmanager                # roll back App Manager updates (back to factory)
adb shell settings put global vr_setupwizard_completed 1      # mark Gear VR setup as done
```
Reinstalling the 7 APKs (the gearvr.net method) wasn't needed in the end. The flag was the real gate. The reinstalls were harmless, because they were the phone's own identical builds.

### 11-10: follow-up, who really updated App Manager
The user couldn't find "Meta App Manager" in the Play Store. `dumpsys package` explains why: the Facebook apps' `installerPackageName` is **com.facebook.system** (Meta's preinstalled "App Installer"), not the Play Store. The timeline:
- 18:05:27: com.facebook.system updated itself to 137.0.26, pulled in by the Samsung VR wizard's own package check (`vas.samsungapps.com … appId=…@com.facebook.system@…`).
- 18:14:24: the new installer pushed App Manager 137.0.26, which broke Oculus.

I didn't disable com.facebook.system, because the VR service lists it among the packages Oculus needs, and a missing one can reset `vr_setupwizard_completed` to 0. Instead I rolled it back to factory with `pm uninstall com.facebook.system`, so it's now **55.1.0**, the same version that ran for years without touching App Manager. Verified afterwards: Oculus starts with no crash, the Gear VR launcher opens, flag=1.

## One-step repair script
`C:\Users\uriol\temp\fix-gearvr.ps1`: connect the phone and run
`powershell -ExecutionPolicy Bypass -File C:\Users\uriol\temp\fix-gearvr.ps1`.
It rolls back App Manager and App Installer only if they were updated, sets the flag only if it isn't 1, then starts Oculus and checks for the crash. Tested 11-10: reported everything OK and changed nothing.

## Keep it working
- Both Meta updaters are now at factory versions (App Manager 55.1.0, App Installer 55.1.0). Nothing needs changing in the Play Store; it doesn't manage these apps. If Oculus ever starts crashing again, uninstall updates of **Meta App Manager** and **Meta App Installer** (Ajustes → Aplicaciones → ⋮ Mostrar aplicaciones del sistema → the app → ⋮ → Desinstalar actualizaciones).
- If the update screen ever returns, the flag was reset. That happens after an OS update or a VR-system package change. Re-run `settings put global vr_setupwizard_completed 1`.
- Don't tap "Actualizar" in the VR setup wizard. It can't succeed.
- Play Protect's ADB-install scan was turned off only during the OCMS reinstalls and is back on (`verifier_verify_adb_installs=1`).

## 2026-11-10 (afternoon): individual apps, Minecraft
**Symptom:** Gear VR works and Ski Jumping VR runs, but Minecraft (com.mojang.minecraftvr 1.16.40.02, armeabi-v7a, installed 2023-02-20) flashes and drops back to the launcher. The Oculus Store shows a placeholder, which is expected since Meta shut it down.

**How I got logs while the phone is in the headset:** ADB over Wi-Fi was refused by Claude Code's safety rules (it exposes a network port). Instead I ran a log recorder on the phone itself, which keeps going after the cable is unplugged:
```
adb shell "logcat -c; nohup logcat -v time -f /sdcard/mc_dock.log -r 8192 -n 3 >/dev/null 2>&1 &"
# ...user tests in headset, plugs back in...
adb shell "pkill -f 'logcat -v time -f /sdcard/mc_dock.log'"; adb pull /sdcard/mc_dock.log
```

**What the log shows (14:48, online):**
- VR mode starts fine (`vrapi_EnterVrMode [end]`, `HMT was mounted`).
- **Oculus entitlement passes**: `EntitlementRequest took 53 ms`, no error. So it's not a licence or ownership problem.
- Xbox Live libraries register, then about 0.2 s later `Breakpad CrashFilterCallback called`, a ~3 s freeze (`Skipped 172 frames`), `Breakpad DumpCallback … .dmp`, and the process dies.
- The minidump is in private app storage (/data/user/0/…), so I can't read it without root.

**Offline test (14:54, Wi-Fi and data off):** identical crash at the same point. **Not a network or dead-server issue.**

**Other facts:** `/sdcard/games/com.mojang` was created today at 14:16 and has no worlds (56 KB total). Package flags: ALLOW_CLEAR_USER_DATA, ALLOW_BACKUP.

**Data reset (user approved):** `pm clear com.mojang.minecraftvr`, then retested in the headset: **same crash**. So it isn't corrupted data either.

**Trying to read the crash dump:** `adb backup -noapk com.mojang.minecraftvr`. The phone forces a backup password because storage is encrypted (the user set one; not recorded here). The backup came back empty twice; Android 10's deprecated backup skipped the app. I stopped there, because even with the dump the crash is inside Minecraft's closed native code.

**Minecraft conclusion (for now):** it gets into VR, passes the Oculus entitlement, then crashes natively about 0.2 s after Xbox Live initialises, both online and offline, and with fresh data. It has never run on this phone (its game folder was first created 2026-11-10 14:16). Likely an incompatibility between this Minecraft build (1.16.40.02) and the Oculus/Gear VR runtime now on the phone. Not fixed.

## 2026-11-10: alternative "store", Gear VR Vault
The Oculus Store is dead (it shows a placeholder). The fan archive **https://vault.gearvr.net** hosts Gear VR and Oculus Go APKs "for entertainment and archival purposes". To install one on Gear VR, download the APK, open it in a file manager and install it. It needs the Gear VR developer options.

**Developer options enabled (15:34)**: Ajustes → Aplicaciones → Gear VR Service → Almacenamiento → Admin almacenam → tap "VR Service Version" 6 times ("You are a developer!"):
- ✅ **Añadir icono a la lista de aplicaciones**: adds a Gear VR Service icon to the phone's app drawer, which can launch any installed VR APK.
- ✅ **Allow VR API without OSIG**: lets sideloaded APKs run without a per-device signature file.
- ⬜ **Modo de desarrollador**: left OFF on purpose. It makes VR apps run on the bare phone without the headset (`vrmode_developer_mode=0`).
- ⬜ Mirroring mode: off.

These two toggles live in Gear VR Service's own protected database (`content://com.samsung.android.hmt.vrsvc.vr/secure`, key `allow_vr_api_permission`), which ADB can't write. That's why I set them through the hidden UI screen.

**Notes:** Play Protect may flag old Vault APKs as "harmful", as it did with OCMS. "Borrar datos" of Gear VR Service would probably reset these toggles. The fix script doesn't touch them.

## 2026-11-10 15:40: more Minecraft tests
- What the user sees ("black screen, then my phone's launcher") is the crash: every log shows `Process com.mojang.minecraftvr has died` at that moment.
- **Samsung sleeping apps / battery restrictions** (a fix from the forums for this symptom): checked; Minecraft and Oculus aren't restricted (standby buckets 10–40, background allowed). Not the cause.
- **Facebook app** (Play Store reinstalled it 14:27 today; Minecraft queries its AttributionIdProvider at startup): disabled it temporarily with `pm disable-user`. **Same crash.** Re-enabled with `pm enable com.facebook.katana`.
- Online: a Minecraft Forum thread (Note 5) describes exactly this symptom and has no fix.

**Correction on "nothing was lost" from the data reset:** `/sdcard/games` (where Minecraft keeps external worlds) didn't exist before 14:16 today, and usage stats show no Minecraft use in 2026 before today (7 launches, 37 s, all today). At 14:48, before the reset, the game was already behaving like a fresh install (it picked external storage and built new empty folders). That suggests its private data was already fresh then. But if earlier worlds (2023–2025) were kept in its private storage, `pm clear` at 14:57 would have deleted them, and I can't rule that out.

## 2026-11-10 16:05: Vault Minecraft, new apps, Vault mirror
- **Vault Minecraft** (https://vault.gearvr.net/app/minecraft/, "Gamepad Required", tested on S8) is **byte-identical** to the installed copy (same SHA-256, signed by Mojang AB), so I didn't reinstall it. Lead: the "Gamepad Required" note fits the log ("INPUT device … is Crete Controller: false" right before the crash). To try next: pair a Bluetooth gamepad or the Gear VR controller.
- **Installed from ~/Downloads/APKS** (at the user's request): Play Protect blocked all three ("Aplicación no segura bloqueada", built for an older Android). I installed them with the ADB scan briefly off, then restored it (`verifier_verify_adb_installs=1`):
  - com.felixandpaul.jurassicworld.apatosaurus (Jurassic World: Apatosaurus)
  - de.muchoviento.vr.astronaut.gear (Astronaut VR)
  - com.felixandpaul.gearvr.jwb (Jurassic World: Blue)
- **Vault mirror to E:\GearVR_Vault** (at the user's request; robots.txt allows /app/ and /product-category/):
  - `scripts\build_catalog.py` (written by a Haiku subagent): crawls /product-category/gear-vr/ and writes `gearvr_catalog.csv`.
  - `scripts\download_vault.py`: one file at a time, 15 s pause, 2 MB/s cap, resumable (.part files, `download_done.txt`), log in `scripts\download_vault.log`.
- **Catalogue** (Haiku agent): 791 Gear VR apps, 818 files (798 vault.gearvr.net, 18 archive.org, 2 broken links on the Vault itself). 0 fetch errors.
- **Download started 16:44** as its own minimized window (python PID 3268), so it survives this session. Test file: 135 MB OK. Estimated ~120 GB, ~20 h. Resume command and notes in `E:\GearVR_Vault\README.txt`.

## 2026-11-11 — The setup loop came back (cause: 12-hour online update check)

- Symptom: "remove the device and update", then the Gear VR software update screen in a loop.
- Meta App Manager and App Installer were still factory 55.1.0. Only `vr_setupwizard_completed` had gone back to 2.
- Cause, found in the vrsvc code (UserSetupWizardUpdateEvent):
  - On every dock, if `last_update_check` is older than 12 h (43,200,000 ms), vrsvc broadcasts `com.samsung.intent.action.REQUEST_VR_APP_UPDATE` to com.samsung.android.app.vrsetupwizard.
  - The wizard's online check, now pointing at dead servers, marks setup as not completed (2).
  - The last check was this morning at 10:31; the loop started right after.
  - The same receiver (`UpdateCheckIntentReceiver`) also fires on BOOT_COMPLETED and CONNECTIVITY_CHANGE.
- vrsvc has a built-in kill switch, `setupwizard_skip_update_check`:
  - It's the hidden dev option **"Skip Update check"** (Gear VR Service → Almacenamiento → Admin almacenam, below "Mirroring mode").
  - When on, vrsvc logs "update check ignored! by feature".
- Tried from adb:
  - Writing the key into vrsvc's secure provider: denied (it needs hmt.permission).
  - `pm disable` of the receiver: "Shell cannot change component state". So it must be toggled on the phone.
  - Scrolling that dev screen with injected input doesn't work either.
- Ran fix-gearvr.ps1: flag 2 → 1.
- Noted: "Modo de desarrollador" is now ON (it was off yesterday).

### Permanent fix: "Skip Update check" (2026-11-11)
- The "Skip Update check" switch is hidden. It only shows for a "skipUpdateCheck tester".
- vrsvc decides that by looking in /sdcard for a file whose name starts with "vr" and whose SHA-256 equals a6bad008…bc165.
- I found the name by brute-forcing candidate names: **`vr.skip.update.check`**. Created it with `adb shell touch /sdcard/vr.skip.update.check`.
- A new section, **SetupWizard Options**, then appeared at the bottom of Admin almacenam, with:
  - Oculus OOBE: setup completed | 1
  - SXR OOBE
  - **Skip Update check**: turned it **ON**. It's still on after a force-stop of vrsvc.
- From now on a dock should log "update check ignored! by feature" instead of running the dead online check.
- Other unlock files found in the same code (not used): "distortion" and "launcher.extra" testers.
- fix-gearvr.ps1 now also re-creates the unlock file. The switch itself is kept in vrsvc's data, so clearing Gear VR Service data would reset it.
- Don't use "Borrar datos" on Gear VR Service.
- "Modo de desarrollador" ON was the user's own choice (they want to develop Gear VR apps). Leave it on.
