# fix-gearvr.ps1: repair Gear VR on the Galaxy S9 (see gearvr-diary.md)
# Run with:  powershell -ExecutionPolicy Bypass -File .\fix-gearvr.ps1
# Safe to run any time: it only changes what's broken.

$adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
if (-not $adb) { $adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" }
if (-not (Test-Path $adb)) { Write-Host "adb not found." -ForegroundColor Red; exit 1 }

function Sh([string]$cmd) { (& $adb shell $cmd) -join "`n" }

# 1. Device connected and authorized?
$state = (& $adb get-state 2>$null)
if ($state -ne 'device') {
    Write-Host "Phone not ready (state: '$state'). Plug it in and accept 'Permitir depuracion USB' on the phone." -ForegroundColor Yellow
    & $adb wait-for-device
}
Write-Host "Phone connected: $(Sh 'getprop ro.product.model')" -ForegroundColor Green

# 2. Roll back Meta App Manager / App Installer if something updated them.
#    Updated copies live in /data/app; factory copies live in /system.
foreach ($pkg in 'com.facebook.appmanager', 'com.facebook.system') {
    $info = Sh "dumpsys package $pkg"
    $ver  = ([regex]::Match($info, 'versionName=(\S+)')).Groups[1].Value
    if ($info -match 'codePath=/data/app') {
        Write-Host "$pkg was updated to $ver, rolling back to factory..." -ForegroundColor Yellow
        & $adb shell pm uninstall $pkg
    } else {
        Write-Host "$pkg OK (factory $ver)"
    }
}

# 2b. Keep Meta App Installer disabled. It's the only Meta app allowed to install
#     updates silently; left enabled, it updates itself and App Manager about once
#     a day, and the new App Manager breaks Oculus ("Caller is not trusted").
#     Gear VR only checks that it is installed, so disabled is fine.
#     App Manager must stay ENABLED: Oculus talks to it.
$fbs = Sh 'dumpsys package com.facebook.system'
if ($fbs -match 'User 0:.*enabled=3') {
    Write-Host "com.facebook.system OK (disabled, can't auto-update)"
} else {
    Write-Host "Disabling com.facebook.system so Meta apps can't auto-update..." -ForegroundColor Yellow
    & $adb shell pm disable-user --user 0 com.facebook.system
}

# 3. Mark Gear VR setup as completed (1 = done; 0 or 2 = shows the update loop).
$flag = (Sh 'settings get global vr_setupwizard_completed').Trim()
if ($flag -ne '1') {
    Write-Host "vr_setupwizard_completed=$flag, setting it to 1..." -ForegroundColor Yellow
    & $adb shell settings put global vr_setupwizard_completed 1
} else {
    Write-Host "vr_setupwizard_completed OK (1)"
}

# 3b. Keep the hidden "Skip Update check" option unlocked. Without it, every dock
#     more than 12 h after the last check runs a dead online update check that resets
#     the flag above. The file only unlocks the switch; the switch itself is in
#     Gear VR Service > Almacenamiento > Admin almacenam > SetupWizard Options.
& $adb shell touch /sdcard/vr.skip.update.check
Write-Host "Unlock file /sdcard/vr.skip.update.check present."
Write-Host "If the loop came back, check that 'Skip Update check' is still ON (Gear VR Service > Almacenamiento > Admin almacenam, bottom)." -ForegroundColor Yellow

# 3c. The VR setup wizard has its own scheduled update check (on boot and every
#     network change, "Solo Wi-Fi" by default) that also resets the flag. It must be
#     "Nunca". It lives in the wizard's private prefs, so we can only open the screen.
Write-Host "Also check: Samsung Gear VR > (menu) Ajustes > Actualizar > Buscar actualizaciones = 'Nunca'." -ForegroundColor Yellow
Write-Host "  Opening it on the phone now..."
& $adb shell am start -n com.samsung.android.app.vrsetupwizard/.ui.LaunchActivity *> $null

# 4. Verify: start Oculus and check that it doesn't crash.
& $adb logcat -c
& $adb shell am force-stop com.oculus.horizon
& $adb shell monkey -p com.oculus.horizon -c android.intent.category.LAUNCHER 1 *> $null
Start-Sleep -Seconds 8
$crash = (& $adb logcat -d) | Select-String 'Caller is not trusted|FATAL EXCEPTION'
& $adb shell input keyevent KEYCODE_HOME

Write-Host ""
if ($crash) {
    Write-Host "Oculus is still crashing:" -ForegroundColor Red
    $crash | Select-Object -First 3 | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "All set. Unplug the phone and put it in the Gear VR." -ForegroundColor Green
    Write-Host "Reminder: never tap 'Actualizar' on the 'Descargar aplicaciones VR' screen."
}
