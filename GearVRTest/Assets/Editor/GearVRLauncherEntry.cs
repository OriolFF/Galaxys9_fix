using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Gear VR's Oculus Home never lists sideloaded apps (only store purchases), and an app
/// started from the phone screen inside the headset runs as a flat 2D window.
///
/// This adds a small 2D launcher activity (Assets/Plugins/Android/VrLaunchActivity.java)
/// to the phone's app drawer. It asks Oculus Home's shell to launch the app in VR (the
/// same path Home uses for store apps). Runs after Oculus Integration's manifest step
/// (callbackOrder 99999), which keeps the Unity activity on Oculus's INFO category.
/// </summary>
class GearVRLauncherEntry : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100000;
    const string Activity = "com.example.gearvrtest.VrLaunchActivity";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        const string ns = "http://schemas.android.com/apk/res/android";
        string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifest)) return;

        var doc = new XmlDocument();
        doc.Load(manifest);
        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("android", ns);
        var app = (XmlElement)doc.SelectSingleNode("/manifest/application");
        if (app == null || app.SelectSingleNode($"activity[@android:name='{Activity}']", nsm) != null) return;

        var act = doc.CreateElement("activity");
        act.SetAttribute("name", ns, Activity);
        act.SetAttribute("label", ns, "@string/app_name");
        act.SetAttribute("theme", ns, "@android:style/Theme.Translucent.NoTitleBar");
        act.SetAttribute("excludeFromRecents", ns, "true");
        act.SetAttribute("noHistory", ns, "true");
        act.SetAttribute("exported", ns, "true");
        var filter = doc.CreateElement("intent-filter");
        var action = doc.CreateElement("action");
        action.SetAttribute("name", ns, "android.intent.action.MAIN");
        var cat = doc.CreateElement("category");
        cat.SetAttribute("name", ns, "android.intent.category.LAUNCHER");
        filter.AppendChild(action);
        filter.AppendChild(cat);
        act.AppendChild(filter);
        app.AppendChild(act);
        doc.Save(manifest);
        Debug.Log("GearVR: added VrLaunchActivity (phone app drawer -> launch in VR via Oculus Home).");
    }
}
