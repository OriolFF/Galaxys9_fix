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

        UseOwnDebugKeystore();
        CreateScene();
        AssetDatabase.SaveAssets();
        Debug.Log("GearVR: project set up.");
    }

    /// <summary>
    /// The shared ~/.android/debug.keystore is in a newer format (made by a newer JDK)
    /// that Unity 2019's Java 8 can't read. Sign with our own debug keystore instead,
    /// created with Unity's own keytool and kept outside the repo. Its password is the
    /// standard, public Android debug password, so it is not a secret.
    /// </summary>
    static void UseOwnDebugKeystore()
    {
        const string pass = "android";
        const string alias = "androiddebugkey";
        string dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".android");
        string ks = Path.Combine(dir, "unity2019-debug.keystore");
        if (!File.Exists(ks))
        {
            Directory.CreateDirectory(dir);
            string keytool = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe");
            var psi = new System.Diagnostics.ProcessStartInfo(keytool,
                "-genkeypair -v -storetype JKS -keystore \"" + ks + "\" -storepass " + pass +
                " -alias " + alias + " -keypass " + pass +
                " -keyalg RSA -keysize 2048 -validity 10000 -dname \"CN=Android Debug,O=Android,C=US\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0) Debug.LogError("GearVR: keytool failed: " + err);
            }
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = ks;
        PlayerSettings.Android.keystorePass = pass;
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = pass;
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
        // Don't leave the local keystore path in ProjectSettings (it would end up in git).
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.keystoreName = "";
        PlayerSettings.Android.keyaliasName = "";
        AssetDatabase.SaveAssets();
        Debug.Log("GearVR: build " + report.summary.result + " -> " + Path.GetFullPath(ApkPath));
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }

    public static void BuildFromCommandLine() => Build();
}
