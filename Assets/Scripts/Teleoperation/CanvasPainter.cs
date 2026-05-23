using UnityEngine;

/// Attach to the canvas object.
/// Assign the TeleopHandProxy transform and a RenderTexture in the Inspector.
/// When pen_down is true and the pointer ray hits this object, paints at the hit UV.
[RequireComponent(typeof(Collider))]
public class CanvasPainter : MonoBehaviour
{
    [Header("References")]
    public Transform handProxy;
    public RenderTexture renderTexture;

    [Header("Brush")]
    public Color brushColor = Color.black;
    [Range(1, 64)]
    public int brushRadiusPx = 6;

    [Header("Ray")]
    [Tooltip("Max raycast distance (m). Set to just cover the pen-to-canvas gap (~0.05 m).")]
    public float maxRayDistanceM = 0.05f;

    private Texture2D _paintTex;
    private bool _penDown = false;

    void Start()
    {
        if (renderTexture == null)
        {
            Debug.LogError("[CanvasPainter] No RenderTexture assigned.");
            return;
        }

        _paintTex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

        // Fill white
        var pixels = new Color[renderTexture.width * renderTexture.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        _paintTex.SetPixels(pixels);
        _paintTex.Apply();

        BlitToRenderTexture();
        GetComponent<Renderer>().material.mainTexture = renderTexture;
    }

    public void SetPenDown(bool value)
    {
        _penDown = value;
    }

    void Update()
    {
        if (!_penDown || handProxy == null || _paintTex == null) return;

        Ray ray = new Ray(handProxy.position, handProxy.up);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistanceM)) return;
        if (hit.collider.gameObject != gameObject) return;

        // BoxCollider doesn't provide valid textureCoord — compute UV from local hit position.
        // Canvas local space has x and y in [-0.5, 0.5]; map to [0, 1].
        Vector3 local = transform.InverseTransformPoint(hit.point);
        int px = Mathf.Clamp((int)((local.x + 0.5f) * _paintTex.width),  0, _paintTex.width  - 1);
        int py = Mathf.Clamp((int)((local.y + 0.5f) * _paintTex.height), 0, _paintTex.height - 1);

        PaintCircle(px, py);
        _paintTex.Apply();
        BlitToRenderTexture();
    }

    void PaintCircle(int cx, int cy)
    {
        int r = brushRadiusPx;
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r) continue;
                int px = cx + x;
                int py = cy + y;
                if (px < 0 || px >= _paintTex.width || py < 0 || py >= _paintTex.height) continue;
                _paintTex.SetPixel(px, py, brushColor);
            }
        }
    }

    void BlitToRenderTexture()
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Graphics.Blit(_paintTex, renderTexture);
        RenderTexture.active = prev;
    }
}
