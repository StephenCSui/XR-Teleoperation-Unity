using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

/// Subscribes to a ROS CompressedImage topic and displays it on a quad in world space.
///
/// Auto-positions above the canvas object (found by name) at startup:
///   - Same X as canvas
///   - 5 cm above canvas top edge
///   - 5 cm back (away from robot, -Z)
///   - 3× canvas size, proportional
///
/// No Inspector wiring needed for position — just attach to any GameObject.
/// Optionally assign a RawImage to displayImage if you want UI-based display instead.
///
/// Control impact: zero.
///   OnImage()   : stores byte reference only (~0 ms, runs in Update)
///   LateUpdate(): decodes after all control Update() calls have published
///   Rate-limited to maxFps — decode fires every N frames only

public class ROSCameraDisplay : MonoBehaviour
{
    [Header("ROS")]
    public string topic = "/workspace_cam/color/image_raw/compressed";

    [Header("Rate")]
    [Tooltip("Max texture updates per second. 10–15 recommended.")]
    public float maxFps = 15f;

    [Header("Layout (auto-computed from canvas — leave at 0 to use defaults)")]
    [Tooltip("Name of the canvas GameObject to position relative to.")]
    public string canvasObjectName = "canvas";
    [Tooltip("Gap between canvas top and screen bottom (m).")]
    public float gapAboveM = 0.10f;
    [Tooltip("How far behind the canvas face to offset the screen (m).")]
    public float offsetBackM = 0.10f;
    [Tooltip("Screen size multiplier relative to canvas.")]
    public float sizeMultiplier = 3f;
    [Tooltip("Fixed screen size in metres — overrides sizeMultiplier if non-zero.")]
    public Vector2 fixedSizeM = new Vector2(0.75f, 0.50f);

    [Header("Optional — wire a UI RawImage instead of auto quad")]
    public RawImage displayImage;

    // ── Private ────────────────────────────────────────────────────────────────

    private byte[]       _latestBytes;
    private readonly object _lock = new object();

    private Texture2D    _tex;
    private float        _nextUpdateTime;
    private GameObject   _screenQuad;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        _tex = new Texture2D(4, 4, TextureFormat.RGB24, false);

        if (displayImage != null)
        {
            // Manual UI mode — use the assigned RawImage
            displayImage.texture = _tex;
        }
        else
        {
            // Auto mode — create a world-space quad positioned above the canvas
            CreateScreenQuad();
        }

        ROSConnection.GetOrCreateInstance()
            .Subscribe<CompressedImageMsg>(topic, OnImage);

        Debug.Log($"[ROSCameraDisplay] Subscribed to {topic}");
    }

    void OnDestroy()
    {
        if (_tex        != null) Destroy(_tex);
        if (_screenQuad != null) Destroy(_screenQuad);
    }

    // ── Auto quad creation ─────────────────────────────────────────────────────

    void CreateScreenQuad()
    {
        GameObject canvasGO = GameObject.Find(canvasObjectName);
        if (canvasGO == null)
        {
            Debug.LogWarning($"[ROSCameraDisplay] Could not find '{canvasObjectName}' — using fallback position.");
            canvasGO = gameObject;   // fall back to this object's position
        }

        // Canvas world-space bounds
        Vector3 pos   = canvasGO.transform.position;
        Vector3 scale = canvasGO.transform.lossyScale;   // (0.25, 0.35, 0.02)

        float canvasW = scale.x;
        float canvasH = scale.y;
        float canvasTopY = pos.y + canvasH * 0.5f;

        // Screen size — fixed if set, otherwise 3× canvas
        float screenW = fixedSizeM.x > 0 ? fixedSizeM.x : canvasW * sizeMultiplier;
        float screenH = fixedSizeM.y > 0 ? fixedSizeM.y : canvasH * sizeMultiplier;

        // Position: same X, above canvas top + gap, slightly back in Z
        Vector3 screenPos = new Vector3(
            pos.x,
            canvasTopY + gapAboveM + screenH * 0.5f,
            pos.z - offsetBackM
        );

        // Create quad — matches canvas orientation
        _screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _screenQuad.name = "ROSCameraScreen";
        _screenQuad.transform.position   = screenPos;
        _screenQuad.transform.rotation   = canvasGO.transform.rotation;
        _screenQuad.transform.localScale  = new Vector3(screenW, screenH, 1f);

        // Remove collider — don't interfere with canvas painting raycasts
        Destroy(_screenQuad.GetComponent<Collider>());

        // URP-compatible unlit material
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");   // fallback
        var mat = new Material(shader);
        mat.mainTexture = _tex;
        _screenQuad.GetComponent<Renderer>().material = mat;

        Debug.Log($"[ROSCameraDisplay] Screen quad created at {screenPos}, size {screenW:F2}x{screenH:F2} m");
    }

    // ── ROS callback (main thread — must be instant) ───────────────────────────

    void OnImage(CompressedImageMsg msg)
    {
        lock (_lock)
            _latestBytes = msg.data;
    }

    // ── Decode pass (after all Update() — control already published) ───────────

    void LateUpdate()
    {
        if (Time.realtimeSinceStartup < _nextUpdateTime) return;

        byte[] bytes;
        lock (_lock)
        {
            bytes        = _latestBytes;
            _latestBytes = null;
        }

        if (bytes == null) return;

        _tex.LoadImage(bytes);

        if (displayImage != null)
            displayImage.texture = _tex;
        else if (_screenQuad != null)
            _screenQuad.GetComponent<Renderer>().sharedMaterial.mainTexture = _tex;

        _nextUpdateTime = Time.realtimeSinceStartup + 1f / Mathf.Max(1f, maxFps);
    }
}
