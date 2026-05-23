using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

/// <summary>
/// Publishes the precision bounding box to ROS.
///
/// Place this script on a GameObject and position that object where you want
/// the precision zone to be.  Adjust boxWidth / boxHeight in the Inspector.
///
/// The ROS filter uses the geometric mean of width and height to compute
/// position_scale: smaller box → finer control (lower scale).
///   Min 2 cm × 2 cm  → tightest (precision_scale_at_min)
///   Max 5 cm × 5 cm  → loosest  (precision_scale_at_max)
///
/// boxDepth controls the into-canvas tolerance (does not affect scale).
///
/// Only publishes while PRECISION mode is active.
/// </summary>
public class PrecisionBoxPublisher : MonoBehaviour
{
    [Header("ROS Topics")]
    public string boxTopic      = "/unity/precision_box";
    public string boxSizeTopic  = "/unity/precision_box_size";
    public string frameId       = "unity_world";

    [Header("Publish Rate")]
    public float publishHz = 10f;

    [Header("Box Size (metres)")]
    [Range(0.02f, 0.05f)]
    public float boxWidth  = 0.03f;   // Unity X axis
    [Range(0.02f, 0.05f)]
    public float boxHeight = 0.03f;   // Unity Y axis
    [Range(0.02f, 0.10f)]
    public float boxDepth  = 0.05f;   // Unity Z axis — into-canvas tolerance

    [Header("References")]
    public TeleopModeController modeController;

    private ROSConnection _ros;
    private float _nextPublish;

    const float kMinSize = 0.02f;
    const float kMaxSize = 0.05f;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<PoseStampedMsg>(boxTopic);
        _ros.RegisterPublisher<Vector3Msg>(boxSizeTopic);
        _nextPublish = Time.realtimeSinceStartup;
    }

    void Update()
    {
        // Only publish in PRECISION mode
        if (modeController != null && !modeController.IsPrecision)
            return;

        if (Time.realtimeSinceStartup < _nextPublish)
            return;

        _nextPublish += 1f / Mathf.Max(1f, publishHz);

        float w = Mathf.Clamp(boxWidth,  kMinSize, kMaxSize);
        float h = Mathf.Clamp(boxHeight, kMinSize, kMaxSize);
        float d = Mathf.Clamp(boxDepth,  kMinSize, 0.10f);

        Vector3 center = transform.position;

        // Box centre as PoseStamped (orientation irrelevant — filter only uses position)
        var header = new HeaderMsg(0u, NowRosTime(), frameId);
        var pose   = new PoseMsg(
            new PointMsg(center.x, center.y, center.z),
            new QuaternionMsg(0.0, 0.0, 0.0, 1.0));
        _ros.Publish(boxTopic, new PoseStampedMsg(header, pose));

        // Box size as Vector3 (x = width, y = height, z = depth)
        _ros.Publish(boxSizeTopic, new Vector3Msg(w, h, d));
    }

    // Always-visible gizmo in Scene view so you can see the zone without selecting the object
    void OnDrawGizmos()
    {
        float w = Mathf.Clamp(boxWidth,  kMinSize, kMaxSize);
        float h = Mathf.Clamp(boxHeight, kMinSize, kMaxSize);
        float d = Mathf.Clamp(boxDepth,  kMinSize, 0.10f);

        Gizmos.color = new Color(0.0f, 0.9f, 0.4f, 0.25f);
        Gizmos.DrawCube(transform.position, new Vector3(w, h, d));
        Gizmos.color = new Color(0.0f, 0.9f, 0.4f, 1.0f);
        Gizmos.DrawWireCube(transform.position, new Vector3(w, h, d));
    }

    static TimeMsg NowRosTime()
    {
        var now  = DateTimeOffset.UtcNow;
        long sec  = now.ToUnixTimeSeconds();
        long nsec = (now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond) * 100L;
        nsec = Math.Clamp(nsec, 0L, 999_999_999L);
        return new TimeMsg((uint)sec, (uint)nsec);
    }
}
