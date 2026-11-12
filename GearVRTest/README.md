# Gear VR Test (Unity 2019.4)

A small test app for the Galaxy S9 and Gear VR. It's a room with a ring of cubes, a gaze reticle and a floating info panel showing FPS, head angles and tap count.

How to use it:
- Look at a cube and tap the headset touchpad. The cube changes colour and spins.
- The back button quits.

## Why Unity 2019.4

Gear VR runs on the Oculus Mobile SDK (VrApi). Unity's built-in Oculus VR support was the last version that could target it, and that support was removed after 2019/2020. Unity 6 can't build Gear VR apps.

Required install: Unity **2019.4.40f1** with Android Build Support (OpenJDK and Android SDK & NDK).

## Official Oculus SDK (Oculus Integration v18.0)

The project uses Meta's official **Oculus Integration v18.0** (August 2020). It's the last version supporting Oculus Go and Gear VR-class devices; Go support was deprecated in v19.1.

It isn't committed to git (about 370 MB of Meta's files). Restore it with:

```powershell
python -I tools/get_oculus_sdk.py            # libraries only
python -I tools/get_oculus_sdk.py --samples  # + SampleFramework (265 MB, mostly Quest samples)
```

The script downloads `OculusIntegration_v18.unitypackage` from Meta's developer archive, caches it in `E:\GearVR_SDK`, and unpacks these into `Assets/Oculus`:

| Folder | What it gives you |
|---|---|
| `VR` | OVRCameraRig, OVRManager, **OVRInput** (Gear VR Controller, headset touchpad, back), OVRPlugin |
| `Platform` | Oculus Platform SDK (entitlements, achievements; the online services are shut down) |
| `Spatializer` | Oculus Audio spatializer |
| `Avatar` | Oculus Avatars |
| `LipSync` | Oculus LipSync |
| `AudioManager` | Sound management helpers |

`Assets/Editor/OculusDefine` sets the `OCULUS_INTEGRATION` scripting define when `Assets/Oculus` is present. With it, the app uses OVRCameraRig and OVRInput: a Gear VR Controller laser, trigger or touchpad click to select, swipes to turn the cube ring, and battery shown on the panel. Without it, the project still compiles and falls back to gaze plus tap.

Downloading the SDK means accepting the [Oculus SDK License](https://developer.oculus.com/licenses/sdk-3.5/).

## Build

From the editor: open `GearVRTest` in Unity 2019.4, then use **GearVR > Build APK**.

From the command line:

```powershell
& "C:\Program Files\Unity\Hub\Editor\2019.4.40f1\Editor\Unity.exe" -batchmode -quit `
  -projectPath GearVRTest -executeMethod GearVRBuild.BuildFromCommandLine -logFile build.log
adb install -r GearVRTest\Builds\GearVRTest.apk
```

The APK is signed with a local debug key, `%USERPROFILE%\.android\unity2019-debug.keystore`. The build script creates it with Unity's Java 8 keytool, because the shared `debug.keystore` made by newer tools can't be read by Unity 2019. That key lives outside the repo and uses the standard public Android debug password. No keystore or real password is stored in this repo.

## Phone requirements

These are set in the hidden Gear VR Service options; see `../gearvr-diary.md`.
- **Allow VR API without OSIG** must be ON. Otherwise the app needs an Oculus signature file for this phone.
- **Modo de desarrollador** ON lets you run the app on the phone screen without docking.

## The scene is built from code

`Assets/Scripts/GearVRTestApp.cs` creates everything at runtime. `Assets/Editor/GearVRBuild.cs` sets the player settings and creates the scene, so there's no hand-edited scene YAML to keep in sync.
