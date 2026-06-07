using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

public class TeleopModeController : MonoBehaviour
{
    [Header("ROS Topics")]
    public string setModeTopic    = "/teleop_mode/set_mode";
    public string activeModeTopic = "/teleop_mode/active_mode";
    public string handPoseTopic   = "/unity/hand_pose";
    public string penDownTopic    = "/unity/pen_down";
    public string eePoseTopic     = "/robot/ee_pose";
    public string frameId         = "unity_world";

    [Header("References")]
    public TeleopHandPosePublisher handPosePublisher;

    [Header("Precision Entry")]
    public float stopPlaneRosX      = -0.42f;
    public float stopPlaneTolerance = 0.03f;

    [Header("Precision Joystick")]
    public float positionSpeedMs = 0.10f;
    public float pitchSpeedDegS  = 30f;
    public float yawSpeedDegS    = 30f;
    public float stickDeadband   = 0.10f;

    public bool IsPrecision => _mode == "PRECISION";

    private ROSConnection _ros;
    private string _mode          = "NORMAL";
    private bool   _orientationMode = false;

    private Vector3    _virtualHandPos;
    private Quaternion _virtualHandRot;

    private PoseStampedMsg _lastEePose;
    private bool           _haveEePose = false;

    private bool _prevAButton = false;
    private bool _prevBButton = false;

    void Start()
    {
        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<StringMsg>(setModeTopic);
        _ros.RegisterPublisher<PoseStampedMsg>(handPoseTopic);
        _ros.RegisterPublisher<BoolMsg>(penDownTopic);
        _ros.Subscribe<StringMsg>(activeModeTopic, msg => _mode = msg.data);
        _ros.Subscribe<PoseStampedMsg>(eePoseTopic, msg => { _lastEePose = msg; _haveEePose = true; });
    }

    void Update()
    {
        bool aDown = XRButtonDown(XRNode.RightHand, CommonUsages.primaryButton,   ref _prevAButton);
        bool bDown = XRButtonDown(XRNode.RightHand, CommonUsages.secondaryButton, ref _prevBButton);

#if !UNITY_ANDROID
        if (Input.GetKeyDown(KeyCode.P))   aDown = true;
        if (Input.GetKeyDown(KeyCode.Tab)) bDown = true;
#endif

        if (aDown)
        {
            if (_mode == "NORMAL")
            {
                if (_haveEePose) EnterPrecision();
            }
            else
            {
                ExitPrecision();
            }
        }

        if (_mode != "PRECISION") return;

        if (bDown)
            _orientationMode = !_orientationMode;

        Vector2 stick = GetThumbstick();
        if (stick.magnitude < stickDeadband) stick = Vector2.zero;

        float speedMult = TriggerHeld() ? 0.5f : 1.0f;

        if (!_orientationMode)
        {
            // Unity X = +ROS Y (canvas left/right), Unity Y = ROS Z (canvas up/down)
            // Z is fixed — virtual hand stays pressed against canvas
            _virtualHandPos.x += stick.x * positionSpeedMs * speedMult * Time.deltaTime;
            _virtualHandPos.y += stick.y * positionSpeedMs * speedMult * Time.deltaTime;
        }
        else
        {
            float pitch = stick.y * pitchSpeedDegS * speedMult * Time.deltaTime;
            float yaw   = stick.x * yawSpeedDegS   * speedMult * Time.deltaTime;
            _virtualHandRot =
                Quaternion.AngleAxis(pitch, Vector3.right) *
                Quaternion.AngleAxis(yaw,   Vector3.up)    *
                _virtualHandRot;
        }

        PublishVirtualPose();
    }

    void EnterPrecision()
    {
        var p = _lastEePose.pose.position;
        var q = _lastEePose.pose.orientation;

        Vector3 rosPos = new Vector3((float)p.x, (float)p.y, (float)p.z);
        _virtualHandPos = TeleopHandPosePublisher.RosToUnityPosition(rosPos);
        // Push virtual Z past touch plane — Unity Z = -ROS X (base rotated 180°)
        // So to target ROS X = stopPlaneRosX - 0.05, Unity Z = -(stopPlaneRosX - 0.05)
        _virtualHandPos.z = -(stopPlaneRosX - 0.30f);  // push well past touch plane; filter clamps

        // Use physical controller rotation — filter sees no discontinuity at mode switch
        _virtualHandRot = handPosePublisher != null
            ? handPosePublisher.transform.rotation
            : Quaternion.identity;

        _orientationMode = false;
        SetMode("PRECISION");
    }

    void ExitPrecision()
    {
        _ros.Publish(penDownTopic, new BoolMsg(false));
        SetMode("NORMAL");
    }

    void SetMode(string mode)
    {
        _mode = mode;
        _ros.Publish(setModeTopic, new StringMsg(mode));
        Debug.Log($"[TeleopMode] Mode → {mode}");
    }

    void PublishVirtualPose()
    {
        var pos = new PointMsg(_virtualHandPos.x, _virtualHandPos.y, _virtualHandPos.z);
        var ori = new QuaternionMsg(_virtualHandRot.x, _virtualHandRot.y, _virtualHandRot.z, _virtualHandRot.w);
        var header = new HeaderMsg(NowRosTime(), frameId);
        _ros.Publish(handPoseTopic, new PoseStampedMsg(header, new PoseMsg(pos, ori)));
    }

    static Vector2 GetThumbstick()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        Vector2 val = Vector2.zero;
        if (devices.Count > 0)
            devices[0].TryGetFeatureValue(CommonUsages.primary2DAxis, out val);
        return val;
    }

    static bool TriggerHeld()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        float val = 0f;
        if (devices.Count > 0)
            devices[0].TryGetFeatureValue(CommonUsages.trigger, out val);
        return val > 0.5f;
    }

    static bool XRButtonDown(XRNode node, InputFeatureUsage<bool> usage, ref bool prev)
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        bool current = false;
        if (devices.Count > 0)
            devices[0].TryGetFeatureValue(usage, out current);
        bool down = current && !prev;
        prev = current;
        return down;
    }

    static TimeMsg NowRosTime()
    {
        DateTimeOffset now  = DateTimeOffset.UtcNow;
        long sec  = now.ToUnixTimeSeconds();
        long nsec = (now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond) * 100;
        if (nsec < 0)         nsec = 0;
        if (nsec > 999999999) nsec = 999999999;
        return new TimeMsg((int)sec, (uint)nsec);
    }

    void OnGUI()
    {
        float height = _mode == "PRECISION" ? 90f : 55f;
        GUI.Box(new Rect(10f, 10f, 230f, height), "Teleop Mode");
        GUI.Label(new Rect(20f, 35f, 210f, 20f), $"Mode: {_mode}");
        if (_mode == "PRECISION")
        {
            GUI.Label(new Rect(20f, 55f, 210f, 20f),
                $"Control: {(_orientationMode ? "ORIENTATION" : "POSITION")}  [B]");
            GUI.Label(new Rect(20f, 72f, 210f, 20f), "Exit: [A]");
        }
    }
}
