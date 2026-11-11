# Gear VR Test (Unity 2019.4)

A small test app for the Galaxy S9 and Gear VR. It's a room with a ring of cubes, a gaze reticle and a floating info panel showing FPS, head angles and tap count.

How to use it:
- Look at a cube and tap the headset touchpad. The cube changes colour and spins.
- The back button quits.

## Why Unity 2019.4

Gear VR runs on the Oculus Mobile SDK (VrApi). Unity's built-in Oculus VR support was the last version that could target it, and that support was removed after 2019/2020. Unity 6 can't build Gear VR apps.

Required install: Unity **2019.4.40f1** with Android Build Support (OpenJDK and Android SDK & NDK).

## Build

From the editor: open `GearVRTest` in Unity 2019.4, then use **GearVR > Build APK**.

From the command line:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2019.4.40f1\Editor\Unity.exe" -batchmode -quit `
  -projectPath GearVRTest -executeMethod GearVRBuild.BuildFromCommandLine -logFile build.log
adb install -r GearVRTest\Builds\GearVRTest.apk
```

The APK is signed with Unity's debug key. No keystore or passwords are stored in this repo.

## Phone requirements

These are set in the hidden Gear VR Service options; see `../gearvr-diary.md`.
- **Allow VR API without OSIG** must be ON. Otherwise the app needs an Oculus signature file for this phone.
- **Modo de desarrollador** ON lets you run the app on the phone screen without docking.

## The scene is built from code

`Assets/Scripts/GearVRTestApp.cs` creates everything at runtime. `Assets/Editor/GearVRBuild.cs` sets the player settings and creates the scene, so there's no hand-edited scene YAML to keep in sync.
