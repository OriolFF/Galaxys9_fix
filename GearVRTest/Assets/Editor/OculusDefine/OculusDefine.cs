using System.IO;
using System.Linq;
using UnityEditor;

/// <summary>
/// Turns the OCULUS_INTEGRATION scripting define on when Meta's Oculus Integration
/// (Assets/Oculus/VR, restored by tools/get_oculus_sdk.py) is present, and off when it
/// isn't. Lives in its own assembly so it still runs when the game code doesn't compile.
/// </summary>
[InitializeOnLoad]
static class OculusDefine
{
    const string Define = "OCULUS_INTEGRATION";

    static OculusDefine()
    {
        bool present = File.Exists("Assets/Oculus/VR/Scripts/OVRInput.cs");
        foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(';').Where(d => d.Length > 0).ToList();
            bool has = defines.Contains(Define);
            if (present == has) continue;
            if (present) defines.Add(Define); else defines.Remove(Define);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
        }
    }
}
