using UnityEngine;

/// <summary>
/// Replaces the default cube mesh with a brush/pen shape: a cylinder body and a sphere tip.
/// The tip sits at local +Y (the pointer/up direction). Attach to CommandBox or ActualBox.
///
/// Inspector setup:
///   CommandBox — Color: (0, 1, 0.09)  Alpha: 1.0  (opaque green)
///   ActualBox  — Color: (0, 0.26, 1)  Alpha: 0.4  (transparent blue)
/// </summary>
public class BrushVisual : MonoBehaviour
{
    [Header("Appearance")]
    public Color color = Color.green;
    [Range(0f, 1f)]
    public float alpha = 1f;

    [Header("Shape (metres)")]
    public float bodyLengthM   = 0.080f;
    public float bodyDiameterM = 0.010f;
    public float tipDiameterM  = 0.008f;

    void Start()
    {
        // Hide the existing cube renderer — keep the transform and any scripts untouched
        var mr = GetComponent<MeshRenderer>();
        if (mr) mr.enabled = false;

        Vector3 ps = transform.lossyScale;

        // Body — Unity cylinder: 2 units tall, 1 unit wide (diameter)
        var body = CreatePart(PrimitiveType.Cylinder);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(
            bodyDiameterM / ps.x,
            bodyLengthM   / (2f * ps.y),   // half-height in local units
            bodyDiameterM / ps.z);
        ApplyMaterial(body);

        // Tip sphere — sits at the +Y end of the body (the canvas-facing end)
        var tip = CreatePart(PrimitiveType.Sphere);
        tip.transform.localScale = new Vector3(
            tipDiameterM / ps.x,
            tipDiameterM / ps.y,
            tipDiameterM / ps.z);
        tip.transform.localPosition = new Vector3(0f, bodyLengthM / (2f * ps.y), 0f);
        ApplyMaterial(tip);
    }

    GameObject CreatePart(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        // Remove collider so brush parts don't interfere with raycasts
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);
        go.transform.SetParent(transform, false);
        return go;
    }

    void ApplyMaterial(GameObject obj)
    {
        var mat = new Material(Shader.Find("Standard"));
        Color c = color;
        c.a = alpha;
        mat.color = c;

        if (alpha < 1f)
        {
            // Standard shader transparent mode
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        obj.GetComponent<MeshRenderer>().material = mat;
    }
}
