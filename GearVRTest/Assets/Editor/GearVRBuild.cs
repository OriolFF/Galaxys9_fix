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

    // Gear VR's Biblioteca is built from the account's store entitlements (OCMS DB),
    // matched to installed apps by package name only. Sideloaded packages are hidden
    // (the "unknown sources" gatekeeper is stuck off now that Meta's servers are gone).
    // So we use the package name of an app this account owns but hasn't installed
    // ("No instaladas"): Biblioteca then lists our build as that app, installed.
    // Owned-but-not-installed candidates found on this phone: com.Armature.FailFactoryGearVRDemo,
    // com.MirrorKey.ElectroBeats, br.com.rilix.rilixcoaster.gearvr, com.clicked.fohland,
    // com.picselica.OceanRift, com.imangi.templerunvr, com.netflix.ninja.vr.
    // Set to "com.example.gearvrtest" for a plain sideload build.
    const string AppId = "com.Armature.FailFactoryGearVRDemo";

    [MenuItem("GearVR/Setup Project")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("GearVR", "Stop Play mode first (press the Play button again), then try again.", "OK");
            throw new System.OperationCanceledException("GearVR: stop Play mode first.");
        }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.companyName = "GearVRTest";
        PlayerSettings.productName = "Gear VR Test";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AppId);
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
        // Set it on the Android group explicitly: PlayerSettings.virtualRealitySupported
        // only affects whichever platform happens to be active at that moment.
        UnityEditorInternal.VR.VREditor.SetVREnabledOnTargetGroup(BuildTargetGroup.Android, true);
        UnityEditorInternal.VR.VREditor.SetVREnabledDevicesOnTargetGroup(BuildTargetGroup.Android, new[] { "Oculus" });
        // "V2 Signing (Quest)" adds <uses-feature android.hardware.vr.headtracking required=true>,
        // which marks the app as Quest-only (6DoF), so Gear VR's Oculus Home hides it.
        PlayerSettings.VROculus.v2Signing = false;
#if OCULUS_INTEGRATION
        // Oculus Integration defaults to Quest, which writes a *required* headtracking
        // feature into the manifest; Gear VR's Oculus Home then hides the app.
        // (OculusProjectConfig.asset lives in the git-ignored Assets/Oculus, so set it here.)
        var oculusConfig = OVRProjectConfig.GetProjectConfig();
        oculusConfig.targetDeviceTypes = new System.Collections.Generic.List<OVRProjectConfig.DeviceType>
            { OVRProjectConfig.DeviceType.GearVrOrGo };
        OVRProjectConfig.CommitProjectConfig(oculusConfig);
        UseBuiltinOVRPlugin();
#endif
        // No VR for the PC editor: avoids the "Oculus/OpenVR failed to initialize" console noise.
        UnityEditorInternal.VR.VREditor.SetVREnabledOnTargetGroup(BuildTargetGroup.Standalone, false);
        UnityEditorInternal.VR.VREditor.SetVREnabledDevicesOnTargetGroup(BuildTargetGroup.Standalone, new string[0]);
#pragma warning restore 618

        // (Not "GUI/Text Shader": Shader.Find returns an editor-only copy that can't be built.
        //  The built-in Arial font brings its own text material.)
        IncludeShaders("Legacy Shaders/Diffuse", "Unlit/Color");
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

    /// <summary>
    /// Adds shaders to GraphicsSettings "Always Included Shaders". The scene is built
    /// at runtime, so nothing in the build references these shaders; without this they
    /// get stripped and everything renders magenta.
    /// </summary>
    static void IncludeShaders(params string[] names)
    {
        var gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(gs);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        // Drop editor-only shaders (they break the build) and empty slots.
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            var obj = list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (obj == null || AssetDatabase.GetAssetPath(obj) == "Library/unity default resources")
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }
        }
        foreach (var n in names)
        {
            var shader = Shader.Find(n);
            if (shader == null) { Debug.LogError("GearVR: shader not found: " + n); continue; }
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
            if (present) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        }
        so.ApplyModifiedProperties();
    }

#if OCULUS_INTEGRATION
    /// <summary>
    /// Keep the SDK's own OVRPlugin 1.50 (VrApi 1.1.35) switched OFF and use the one in
    /// Unity's Oculus package instead. The phone's Oculus system driver is older and
    /// rejects 1.1.35 ("Requested API version ... incompatible"), and the app quits.
    /// Also stop OVRPluginUpdater from offering to switch it back on.
    /// </summary>
    static void UseBuiltinOVRPlugin()
    {
        PlayerPrefs.SetInt("Oculus_Utilities_OVRPluginUpdater_AutoUpdate_" + OVRManager.utilitiesVersion, 0);
        bool changed = false;
        foreach (var dir in Directory.GetDirectories("Assets/Oculus/VR/Plugins"))
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(f);
                if (!name.StartsWith("OVRPlugin.") || name.EndsWith(".meta") || name.EndsWith(".disabled"))
                    continue;
                File.Move(f, f + ".disabled");
                if (File.Exists(f + ".meta")) File.Move(f + ".meta", f + ".disabled.meta");
                changed = true;
            }
        if (changed)
        {
            AssetDatabase.Refresh();
            Debug.Log("GearVR: switched off the SDK's OVRPlugin 1.50; using Unity's built-in OVRPlugin.");
        }
    }
#endif

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
