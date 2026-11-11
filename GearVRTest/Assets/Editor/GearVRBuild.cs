using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-click setup and build for the Gear VR test app.
/// Menu: GearVR > Setup Project / Build APK.
/// Command line: Unity.exe -batchmode -quit -projectPath GearVRTest -executeMethod GearVRBuild.BuildFromCommandLine
/// </summary>
public static class GearVRBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string ApkPath = "Builds/GearVRTest.apk";

    [MenuItem("GearVR/Setup Project")]
    public static void Setup()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.companyName = "GearVRTest";
        PlayerSettings.productName = "Gear VR Test";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.example.gearvrtest");
        PlayerSettings.bundleVersion = "0.1";
        PlayerSettings.Android.bundleVersionCode = 1;

        // Gear VR: Android 7+ phones, 32-bit ARM, OpenGL ES 3, landscape.
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        QualitySettings.antiAliasing = 2;

        // Built-in (legacy) VR with the Oculus SDK: this is what drives Gear VR.
#pragma warning disable 618
        PlayerSettings.virtualRealitySupported = true;
        UnityEditorInternal.VR.VREditor.SetVREnabledDevicesOnTargetGroup(BuildTargetGroup.Android, new[] { "Oculus" });
#pragma warning restore 618

        CreateScene();
        AssetDatabase.SaveAssets();
        Debug.Log("GearVR: project set up.");
    }

    static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera") { tag = "MainCamera" };
        cam.AddComponent<Camera>();
        cam.AddComponent<AudioListener>();
        new GameObject("App").AddComponent<GearVRTestApp>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    [MenuItem("GearVR/Build APK")]
    public static void Build()
    {
        Setup();
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.Development,
        });
        Debug.Log("GearVR: build " + report.summary.result + " -> " + Path.GetFullPath(ApkPath));
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }

    public static void BuildFromCommandLine() => Build();
}
