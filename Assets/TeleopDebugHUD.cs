using UnityEngine;
using TMPro;

public class TeleopDebugHUD : MonoBehaviour
{
    [Header("Display")]
    public float updateHz = 0.5f;
    public Vector3 localOffset = new Vector3(0.05f, 0.05f, 0.0f);
    public float panelScale = 0.0015f;
    public int   fontSize   = 18;

    private TextMeshPro _tmp;
    private float       _nextUpdate;

    void Start()
    {
        var panel = new GameObject("DebugHUD");
        panel.transform.SetParent(transform, false);
        panel.transform.localPosition = localOffset;
        panel.transform.localRotation = Quaternion.identity;
        panel.transform.localScale    = Vector3.one * panelScale;

        _tmp = panel.AddComponent<TextMeshPro>();
        _tmp.fontSize        = fontSize;
        _tmp.color           = Color.white;
        _tmp.alignment       = TextAlignmentOptions.TopLeft;
        _tmp.rectTransform.sizeDelta = new Vector2(400, 120);

        _nextUpdate = Time.realtimeSinceStartup;
    }

    void Update()
    {
        if (Time.realtimeSinceStartup < _nextUpdate) return;
        _nextUpdate += 1.0f / Mathf.Max(0.1f, updateHz);

        // Raw Unity forward, remapped to ROS axes: (x_ros, y_ros, z_ros) = (z_u, -x_u, y_u)
        // Only axis swaps and sign flips — no quaternion math.
        Vector3 f = transform.forward;
        float rx =  f.z;
        float ry = -f.x;
        float rz =  f.y;

        _tmp.text = $"hand[ROS]:\n({rx:F3}, {ry:F3}, {rz:F3})";
    }
}
