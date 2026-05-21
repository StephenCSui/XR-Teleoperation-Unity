using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

public class TeleopHandPosePublisher : MonoBehaviour
{
    [Header("ROS Topics")]
    public string poseTopic = "/unity/hand_pose";
    public string teleopTopic = "/unity/teleop_enabled";
    public string eePoseTopic = "/robot/ee_pose";
    public string penDownTopic = "/unity/pen_down";
    public string frameId = "unity_world";

    [Header("Publish")]
    public float publishHz = 60f;
    public bool teleopEnabled = true;

    [Header("Startup Snap")]
    public Transform xrOrigin;
    public bool snapXROriginOnStart = true;
    public bool publishOnlyAfterSnap = true;
    public float eeTimeoutSec = 1.0f;

    [Header("Temporary Input")]
    public bool allowKeyboardToggle = true;
    public KeyCode teleopToggleKey = KeyCode.T;

    [Header("Visuals")]
    [Tooltip("Pitch the HandVisual child to align with natural controller pointing direction (degrees, positive = tilt down).")]
    public float handVisualPitchDeg = -35f;
    public string handVisualChildName = "HandVisual";
    public bool showPointerRay = true;
    public float pointerLengthM = 1.0f;
    public Color pointerColor = new Color(0.2f, 0.8f, 1f);
    public Transform pointerRayParent;  // if set, ray attaches here instead of the hand

    [Header("Canvas Drawing")]
    public CanvasPainter canvasPainter;

    [Header("Mode Control")]
    public TeleopModeController modeController;

    [Header("Debug")]
    public bool debugRawQuaternion = false;
    public float rawDebugPrintHz = 4f;

    private ROSConnection ros;
    private float nextPublishTime;
    private float nextRawDebugTime;

    private bool prevTeleopEnabled = false;
    private bool haveHandAnchor = false;
    private Quaternion handAnchorRotationUnity;

    private bool haveSnapped = false;
    private float lastEeReceiveTime = -1f;

    private bool penDown = false;
    private List<InputDevice> _xrDevices = new List<InputDevice>();

    private LineRenderer _pointerLine;
    private Transform    _pointerParent;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseStampedMsg>(poseTopic);
        ros.RegisterPublisher<BoolMsg>(teleopTopic);
        ros.RegisterPublisher<BoolMsg>(penDownTopic);
        ros.Subscribe<PoseStampedMsg>(eePoseTopic, OnEePoseReceived);

        nextPublishTime  = Time.realtimeSinceStartup;
        nextRawDebugTime = Time.realtimeSinceStartup;
        prevTeleopEnabled = teleopEnabled;

        SetupVisuals();
    }

    void SetupVisuals()
    {
        if (!showPointerRay) return;

        var rayObj = new GameObject("PointerRay");
        Transform parent = pointerRayParent != null ? pointerRayParent : transform;
        rayObj.transform.SetParent(parent, false);
        rayObj.transform.localRotation = Quaternion.identity;

        var lr = rayObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = false;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.up * (pointerLengthM / parent.lossyScale.y));
        lr.startWidth = 0.004f;
        lr.endWidth   = 0.001f;
        lr.material   = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = pointerColor;
        lr.endColor   = new Color(pointerColor.r, pointerColor.g, pointerColor.b, 0f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;

        _pointerLine   = lr;
        _pointerParent = parent;
    }

    void OnEePoseReceived(PoseStampedMsg msg)
    {
        lastEeReceiveTime = Time.realtimeSinceStartup;

        if (!snapXROriginOnStart || haveSnapped || xrOrigin == null)
            return;

        Vector3 rosPos = new Vector3(
            (float)msg.pose.position.x,
            (float)msg.pose.position.y,
            (float)msg.pose.position.z
        );
        Vector3 eeUnity = RosToUnityPosition(rosPos);

        // Rotation snap: align headset forward with Unity +Z (toward robot/canvas)
        // Must run before position snap — rotating the rig shifts where the controller
        // sits in world space, so the position delta must be recalculated after.
        Camera cam = xrOrigin.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            Vector3 camFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (camFwd.sqrMagnitude > 0.001f)
            {
                float yaw = Vector3.SignedAngle(camFwd.normalized, Vector3.forward, Vector3.up);
                xrOrigin.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * xrOrigin.rotation;
                Debug.Log($"[TeleopHandProxy] XROrigin yaw-corrected — yaw={yaw:F1} deg");
            }
        }

        // Position snap: move XR origin so controller lands at EE position.
        // Uses transform.position after rotation so the delta is correct.
        Vector3 delta = eeUnity - transform.position;
        xrOrigin.position += delta;

        haveSnapped = true;
        Debug.Log($"[TeleopHandProxy] XROrigin snapped — pos_delta={delta:F3}");

        // Visual: set HandVisual initial orientation to match EE so all three boxes
        // (ActualBox, CommandBox, HandVisual) look aligned at startup.
        Transform handVisual = transform.Find(handVisualChildName);
        if (handVisual != null)
        {
            Quaternion rosRot = new Quaternion(
                (float)msg.pose.orientation.x,
                (float)msg.pose.orientation.y,
                (float)msg.pose.orientation.z,
                (float)msg.pose.orientation.w
            );
            handVisual.rotation = RosToUnityRotation(rosRot);
        }
    }

    void Update()
    {
        // If EE messages stop, reset snap so the next reconnect triggers a fresh snap
        if (haveSnapped && lastEeReceiveTime >= 0f &&
            Time.realtimeSinceStartup - lastEeReceiveTime > eeTimeoutSec)
        {
            Debug.Log("[TeleopHandProxy] EE timeout — resetting snap for reconnect");
            haveSnapped       = false;
            lastEeReceiveTime = -1f;
            Transform hv = transform.Find(handVisualChildName);
            if (hv != null) hv.localRotation = Quaternion.identity;
        }

        if (publishOnlyAfterSnap && snapXROriginOnStart && !haveSnapped)
            return;

        HandleTeleopToggle();
        UpdateAnchorLatch();

        ReadTrigger();
        if (canvasPainter != null) canvasPainter.SetPenDown(penDown);

        float period = 1.0f / Mathf.Max(1.0f, publishHz);
        if (Time.realtimeSinceStartup >= nextPublishTime)
        {
            nextPublishTime += period;
            // TeleopModeController owns publishing during PRECISION mode
            if (modeController != null && modeController.IsPrecision) return;
            PublishTeleop();
            PublishPoseStamped();
            ros.Publish(penDownTopic, new BoolMsg(penDown));
        }

        if (debugRawQuaternion)
        {
            float rawPeriod = 1.0f / Mathf.Max(0.1f, rawDebugPrintHz);
            if (Time.realtimeSinceStartup >= nextRawDebugTime)
            {
                nextRawDebugTime += rawPeriod;
                PrintRawQuaternionDebug();
            }
        }

        UpdatePointerRayLength();
    }

    void UpdatePointerRayLength()
    {
        if (_pointerLine == null || _pointerParent == null) return;

        float maxLen = pointerLengthM;
        float hitLen = maxLen;

        Ray ray = new Ray(_pointerParent.position, _pointerParent.up);
        if (Physics.Raycast(ray, out RaycastHit hit, maxLen))
            hitLen = hit.distance;

        // Convert world hit distance to local scale along parent's Y axis
        float localLen = hitLen / _pointerParent.lossyScale.y;
        _pointerLine.SetPosition(1, Vector3.up * localLen);
    }

    void ReadTrigger()
    {
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, _xrDevices);
        if (_xrDevices.Count > 0)
        {
            _xrDevices[0].TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
            penDown = trigger;
        }
#if !UNITY_ANDROID
        penDown = penDown || Input.GetKey(KeyCode.P);
#endif
    }

    void HandleTeleopToggle()
    {
#if !UNITY_ANDROID
        if (!allowKeyboardToggle) return;
        if (Input.GetKeyDown(teleopToggleKey))
        {
            teleopEnabled = !teleopEnabled;
            Debug.Log("[TeleopHandProxy] Teleop enabled: " + teleopEnabled);
        }
#endif
    }

    void UpdateAnchorLatch()
    {
        if (teleopEnabled && !prevTeleopEnabled)
        {
            handAnchorRotationUnity = transform.rotation;
            haveHandAnchor = true;
            Debug.Log("[TeleopHandProxy] Hand orientation anchor latched");
        }
        if (!teleopEnabled && prevTeleopEnabled)
            haveHandAnchor = false;

        prevTeleopEnabled = teleopEnabled;
    }

    void PublishTeleop()
    {
        ros.Publish(teleopTopic, new BoolMsg(teleopEnabled));
    }

    void PublishPoseStamped()
    {
        Vector3 p = transform.position;
        Quaternion q = transform.rotation;

        var pos = new PointMsg(p.x, p.y, p.z);
        var ori = new QuaternionMsg(q.x, q.y, q.z, q.w);
        var header = new HeaderMsg(NowRosTime(), frameId);
        var pose = new PoseMsg(pos, ori);

        ros.Publish(poseTopic, new PoseStampedMsg(header, pose));
    }

    void PrintRawQuaternionDebug()
    {
        Quaternion qAbs = transform.rotation;
        Quaternion qRelLocal = haveHandAnchor
            ? Quaternion.Inverse(handAnchorRotationUnity) * qAbs
            : Quaternion.identity;

        Debug.Log(
            $"[UNITY RAW HAND] " +
            $"abs_u=({qAbs.x:F4}, {qAbs.y:F4}, {qAbs.z:F4}, {qAbs.w:F4}) " +
            $"rel_u=({qRelLocal.x:F4}, {qRelLocal.y:F4}, {qRelLocal.z:F4}, {qRelLocal.w:F4}) " +
            $"teleop={(teleopEnabled ? 1 : 0)} anchor={(haveHandAnchor ? 1 : 0)}"
        );
    }

    public static Vector3 RosToUnityPosition(Vector3 ros)
    {
        return new Vector3(-ros.y, ros.z, ros.x);
    }

    public static Quaternion RosToUnityRotation(Quaternion q)
    {
        float n = Mathf.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
        if (n < 1e-6f) return Quaternion.identity;
        q = new Quaternion(q.x/n, q.y/n, q.z/n, q.w/n);
        var r = new Quaternion(-q.y, q.z, q.x, -q.w);
        n = Mathf.Sqrt(r.x*r.x + r.y*r.y + r.z*r.z + r.w*r.w);
        if (n < 1e-6f) return Quaternion.identity;
        return new Quaternion(r.x/n, r.y/n, r.z/n, r.w/n);
    }

    static TimeMsg NowRosTime()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long sec  = now.ToUnixTimeSeconds();
        long ticksWithinSecond = now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond;
        long nsec  = ticksWithinSecond * 100;
        if (nsec < 0)         nsec = 0;
        if (nsec > 999999999) nsec = 999999999;
        return new TimeMsg((int)sec, (uint)nsec);
    }
}
