using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Gear VR test app. Builds the whole scene from code:
/// a floor, a ring of cubes, a gaze reticle and a floating info panel.
/// Look at a cube and tap the headset touchpad: it changes colour and spins.
/// The headset back button quits.
/// </summary>
public class GearVRTestApp : MonoBehaviour
{
    const int CubeCount = 8;
    const float RingRadius = 4f;

    Camera cam;
    Transform reticle;
    TextMesh info;
    Renderer gazed;
    int taps;
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

        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        // In VR the headset overwrites the camera's own position/rotation every frame,
        // so put the camera inside a rig at standing eye height and move the rig instead.
        var rig = new GameObject("CameraRig").transform;
        rig.position = new Vector3(0f, 1.6f, 0f);
        cam.transform.SetParent(rig, false);
        cam.transform.localPosition = Vector3.zero;
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

        for (int i = 0; i < CubeCount; i++)
        {
            float a = i * Mathf.PI * 2f / CubeCount;
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cube " + i;
            cube.transform.position = new Vector3(Mathf.Sin(a) * RingRadius, 1.6f, Mathf.Cos(a) * RingRadius);
            cube.transform.localScale = Vector3.one * 0.7f;
            cube.GetComponent<Renderer>().material = Mat(Color.HSVToRGB((float)i / CubeCount, 0.6f, 0.9f));
            cube.AddComponent<Spinner>();
        }

        // Gaze reticle: a small sphere kept in front of the camera.
        var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(r.GetComponent<Collider>());
        r.name = "Reticle";
        r.transform.localScale = Vector3.one * 0.03f;
        r.GetComponent<Renderer>().material = Mat(Color.white, lit: false);
        reticle = r.transform;

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

    void Update()
    {
        // Back button on the headset arrives as Escape.
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        Renderer hit = null;
        if (Physics.Raycast(ray, out RaycastHit h, 50f))
        {
            hit = h.collider.GetComponent<Renderer>();
            reticle.position = h.point - ray.direction * 0.05f;
        }
        else
        {
            reticle.position = ray.origin + ray.direction * 3f;
        }
        gazed = hit;

        // Touchpad tap (and screen tap when testing on the phone) arrives as mouse button 0.
        if (Input.GetMouseButtonDown(0))
        {
            taps++;
            if (gazed != null)
            {
                gazed.material.color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.8f, 1f);
                gazed.GetComponent<Spinner>().Kick();
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
            "Gear VR test\n{0:0} fps   VR: {1}\nyaw {2:0}  pitch {3:0}\ntaps {4}   looking at: {5}",
            fps, XRSettings.enabled ? XRSettings.loadedDeviceName : "off",
            e.y, e.x > 180f ? e.x - 360f : e.x, taps, gazed != null ? gazed.name : "-");
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
