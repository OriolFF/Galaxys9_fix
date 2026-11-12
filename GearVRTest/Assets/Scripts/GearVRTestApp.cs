using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Gear VR test app. Builds the whole scene from code: a floor, a ring of cubes,
/// a pointer and a floating info panel.
///
/// With Meta's Oculus Integration present (OCULUS_INTEGRATION, see tools/get_oculus_sdk.py):
///   - OVRCameraRig + OVRManager drive the camera (the official Oculus rig).
///   - Gear VR Controller: a laser points from the controller; trigger or touchpad click selects.
///   - Headset touchpad: tap selects; swipe left/right turns the cube ring.
///   - Back button quits. The panel shows controller type and battery.
/// Without it: gaze pointer, screen/touchpad tap selects, back (Escape) quits.
/// </summary>
public class GearVRTestApp : MonoBehaviour
{
    const int CubeCount = 8;
    const float RingRadius = 4f;

    Camera cam;
    Transform rig, ring, reticle, laser;
    LineRenderer laserLine;
    TextMesh info;
    Renderer pointed;
    int taps;
    string lastInput = "-";
    float fps, fpsTimer;
    int frames;

    // Shaders must be listed in GraphicsSettings "Always Included Shaders"
    // (GearVRBuild does this); objects made at runtime don't pull them into the build.
    static Shader litShader, unlitShader;

    static Material Mat(Color c, bool lit = true)
    {
        if (litShader == null) litShader = Shader.Find("Legacy Shaders/Diffuse");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
        return new Material(lit ? litShader : unlitShader) { color = c };
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        BuildRig();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
        cam.nearClipPlane = 0.05f;

        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(3f, 1f, 3f);
        floor.GetComponent<Renderer>().material = Mat(new Color(0.25f, 0.28f, 0.3f));

        ring = new GameObject("CubeRing").transform;
        for (int i = 0; i < CubeCount; i++)
        {
            float a = i * Mathf.PI * 2f / CubeCount;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cube " + i;
            cube.transform.SetParent(ring);
            cube.transform.position = new Vector3(Mathf.Sin(a) * RingRadius, 1.6f, Mathf.Cos(a) * RingRadius);
            cube.transform.localScale = Vector3.one * 0.7f;
            cube.GetComponent<Renderer>().material = Mat(Color.HSVToRGB((float)i / CubeCount, 0.6f, 0.9f));
            cube.AddComponent<Spinner>();
        }

        // Pointer end: a small sphere where the gaze / controller ray hits.
        var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(r.GetComponent<Collider>());
        r.name = "Reticle";
        r.transform.localScale = Vector3.one * 0.03f;
        r.GetComponent<Renderer>().material = Mat(Color.white, lit: false);
        reticle = r.transform;

        // Laser from the Gear VR Controller (hidden while no controller is connected).
        laser = new GameObject("Laser").transform;
        laserLine = laser.gameObject.AddComponent<LineRenderer>();
        laserLine.material = Mat(new Color(0.3f, 0.8f, 1f), lit: false);
        laserLine.startWidth = 0.006f;
        laserLine.endWidth = 0.002f;
        laserLine.positionCount = 2;
        laserLine.enabled = false;

        // Floating info panel in front of the start position.
        var panel = new GameObject("Info");
        panel.transform.position = new Vector3(0f, 2.3f, 2.5f);
        info = panel.AddComponent<TextMesh>();
        // A TextMesh made from code has no font: use Unity's built-in Arial.
        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        info.font = font;
        panel.GetComponent<MeshRenderer>().material = font.material;
        info.characterSize = 0.04f;
        info.fontSize = 48;
        info.anchor = TextAnchor.MiddleCenter;
        info.alignment = TextAlignment.Center;
        info.color = Color.white;
    }

    void BuildRig()
    {
        var old = Camera.main;
#if OCULUS_INTEGRATION
        // Official Oculus rig: OVRCameraRig creates TrackingSpace/eye/hand anchors and
        // its own camera; OVRManager handles tracking, recentering and performance levels.
        if (old != null) Destroy(old.gameObject);
        var go = new GameObject("OVRCameraRig");
        go.SetActive(false);                 // add components before Awake runs
        go.transform.position = new Vector3(0f, 1.6f, 0f);
        var manager = go.AddComponent<OVRManager>();
        manager.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;
        var ovrRig = go.AddComponent<OVRCameraRig>();
        ovrRig.usePerEyeCameras = false;
        go.SetActive(true);
        rig = go.transform;
        cam = ovrRig.centerEyeAnchor.GetComponent<Camera>();
        cam.tag = "MainCamera";
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
#else
        // In VR the headset overwrites the camera's own position/rotation every frame,
        // so put the camera inside a rig at standing eye height and move the rig instead.
        cam = old != null ? old : new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
        rig = new GameObject("CameraRig").transform;
        rig.position = new Vector3(0f, 1.6f, 0f);
        cam.transform.SetParent(rig, false);
        cam.transform.localPosition = Vector3.zero;
#endif
    }

    void Update()
    {
        bool select, back;
        float turn = 0f;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        string controller = "headset";
        int battery = -1;

#if OCULUS_INTEGRATION
        var remote = OVRInput.IsControllerConnected(OVRInput.Controller.RTrackedRemote) ? OVRInput.Controller.RTrackedRemote
                   : OVRInput.IsControllerConnected(OVRInput.Controller.LTrackedRemote) ? OVRInput.Controller.LTrackedRemote
                   : OVRInput.Controller.None;
        if (remote != OVRInput.Controller.None)
        {
            // Controller pose is relative to the rig's tracking space.
            var space = rig.Find("TrackingSpace") ?? rig;
            Vector3 p = space.TransformPoint(OVRInput.GetLocalControllerPosition(remote));
            Quaternion q = space.rotation * OVRInput.GetLocalControllerRotation(remote);
            ray = new Ray(p, q * Vector3.forward);
            controller = remote == OVRInput.Controller.RTrackedRemote ? "Gear VR Controller (R)" : "Gear VR Controller (L)";
            battery = OVRInput.GetControllerBatteryPercentRemaining(remote);
        }
        select = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)   // controller trigger
              || OVRInput.GetDown(OVRInput.Button.One)                   // controller/headset touchpad click
              || Input.GetMouseButtonDown(0);                            // phone screen (dev mode)
        back = OVRInput.GetDown(OVRInput.Button.Back) || Input.GetKeyDown(KeyCode.Escape);
        if (OVRInput.GetDown(OVRInput.Button.DpadLeft)) { turn = -45f; lastInput = "swipe left"; }
        if (OVRInput.GetDown(OVRInput.Button.DpadRight)) { turn = 45f; lastInput = "swipe right"; }
        if (OVRInput.GetDown(OVRInput.Button.DpadUp)) lastInput = "swipe up";
        if (OVRInput.GetDown(OVRInput.Button.DpadDown)) lastInput = "swipe down";
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)) lastInput = "trigger";
        else if (OVRInput.GetDown(OVRInput.Button.One)) lastInput = "touchpad click";
#else
        // Touchpad tap (and screen tap when testing on the phone) arrives as mouse button 0.
        select = Input.GetMouseButtonDown(0);
        back = Input.GetKeyDown(KeyCode.Escape);
        if (select) lastInput = "tap";
#endif
        if (back) Application.Quit();
        if (turn != 0f) ring.Rotate(0f, turn, 0f);

        pointed = null;
        Vector3 end = ray.origin + ray.direction * 3f;
        if (Physics.Raycast(ray, out RaycastHit h, 50f))
        {
            pointed = h.collider.GetComponent<Renderer>();
            end = h.point - ray.direction * 0.05f;
        }
        reticle.position = end;
        laserLine.enabled = controller != "headset";
        if (laserLine.enabled)
        {
            laserLine.SetPosition(0, ray.origin);
            laserLine.SetPosition(1, end);
        }

        if (select)
        {
            taps++;
            if (pointed != null && pointed.GetComponent<Spinner>() != null)
            {
                pointed.material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);
                pointed.GetComponent<Spinner>().Kick();
            }
        }

        frames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer >= 0.5f)
        {
            fps = frames / fpsTimer;
            frames = 0;
            fpsTimer = 0f;
        }

        Vector3 e = cam.transform.eulerAngles;
        info.text = string.Format(
            "Gear VR test{0}\n{1:0} fps   VR: {2}\nyaw {3:0}  pitch {4:0}\ninput: {5}{6}\nlast: {7}   taps {8}\npointing at: {9}",
#if OCULUS_INTEGRATION
            " (Oculus SDK)",
#else
            "",
#endif
            fps, XRSettings.enabled ? XRSettings.loadedDeviceName : "off",
            e.y, e.x > 180f ? e.x - 360f : e.x,
            controller, battery >= 0 ? "  battery " + battery + "%" : "",
            lastInput, taps, pointed != null ? pointed.name : "-");
    }
}

/// <summary>Slow idle rotation, with a fast spin after a tap.</summary>
public class Spinner : MonoBehaviour
{
    float boost;

    public void Kick() { boost = 720f; }

    void Update()
    {
        transform.Rotate(0f, (20f + boost) * Time.deltaTime, 0f, Space.World);
        boost = Mathf.MoveTowards(boost, 0f, 600f * Time.deltaTime);
    }
}
