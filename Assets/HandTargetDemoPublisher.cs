using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

public class HandTargetDemoPublisher : MonoBehaviour
{
    [Header("ROS Topics")]
    public string poseTopic = "/unity/hand_pose";
    public string teleopTopic = "/unity/teleop_enabled";
    public string eePoseTopic = "/robot/ee_pose";
    public string frameId = "unity_world";

    [Header("Publish")]
    public float publishHz = 60f;

    [Header("Keyboard Motion")]
    public float moveSpeed = 0.30f;
    public float rotateSpeedDeg = 90f;

    [Header("Startup Sync")]
    public bool snapToRobotEeOnStart = true;
    public bool useRobotEeRotationOnStart = false;
    public bool publishOnlyAfterInitialEe = true;

    [Header("VR Alignment Mode")]
    public bool useOffsetAlignment = false;
    public KeyCode alignKey = KeyCode.R;

    [Header("Debug")]
    public bool debugRosOrientation = true;
    public float debugPrintHz = 4f;

    [Header("Raw Quaternion Debug")]
    public bool debugRawQuaternion = true;
    public float rawDebugPrintHz = 4f;

    private ROSConnection ros;
    private float nextPublishTime;
    private float nextDebugTime;
    private float nextRawDebugTime;

    [Header("Connection")]
    public float eeTimeoutSec = 1.0f;
    public float eeWaitTimeoutSec = 3.0f;  // give up waiting for EE after this long

    private bool haveInitialEe = false;
    private float startTime = -1f;
    private bool pendingEeApply = false;
    private float lastEeReceiveTime = -1f;

    private Vector3 pendingUnityPosition;
    private Quaternion pendingUnityRotation;

    private Vector3 _alignOffset = Vector3.zero;
    private Vector3 _latestEeUnity;

    void OnEnable()
    {
        haveInitialEe  = false;
        pendingEeApply = false;
        _alignOffset   = Vector3.zero;
        startTime      = -1f;
    }

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        startTime = Time.realtimeSinceStartup;

        ros.RegisterPublisher<PoseStampedMsg>(poseTopic);
        ros.RegisterPublisher<BoolMsg>(teleopTopic);
        ros.Subscribe<PoseStampedMsg>(eePoseTopic, OnEePoseReceived);

        nextPublishTime   = Time.realtimeSinceStartup;
        nextDebugTime     = Time.realtimeSinceStartup;
        nextRawDebugTime  = Time.realtimeSinceStartup;
    }

    void OnDisable()
    {
        // Tell the ROS filter teleop is off before the connection drops.
        // Prevents the filter's anchor from surviving into the next session.
        if (ros != null)
            ros.Publish(teleopTopic, new BoolMsg(false));
    }

    void Update()
    {
        // Detect within-session TCP drop: if EE messages stop arriving, reset snap gate
        // so the next reconnect triggers a fresh hand snap before publishing resumes.
        if (haveInitialEe && lastEeReceiveTime >= 0f &&
            Time.realtimeSinceStartup - lastEeReceiveTime > eeTimeoutSec)
        {
            Debug.Log("[HandTarget] EE pose timeout — resetting snap gate for reconnect");
            haveInitialEe     = false;
            pendingEeApply    = false;
            lastEeReceiveTime = -1f;
        }

        ApplyPendingEePoseIfAny();

        if (useOffsetAlignment && haveInitialEe && Input.GetKeyDown(alignKey))
        {
            _alignOffset = _latestEeUnity - transform.position;
            Debug.Log($"[HandTarget] VR re-aligned to EE, offset={_alignOffset:F3}");
        }

        if (publishOnlyAfterInitialEe && (snapToRobotEeOnStart || useOffsetAlignment) && !haveInitialEe)
        {
            if (startTime >= 0f && Time.realtimeSinceStartup - startTime < eeWaitTimeoutSec)
                return;
            // Timed out waiting for EE — allow keyboard control without snap
            Debug.Log("[HandTarget] EE wait timed out — proceeding without snap");
            haveInitialEe = true;
        }

        HandleKeyboard();

        float period = 1.0f / Mathf.Max(1.0f, publishHz);
        if (Time.realtimeSinceStartup >= nextPublishTime)
        {
            nextPublishTime += period;
            PublishTeleop();
            PublishPoseStamped();
        }

        if (debugRosOrientation)
        {
            float debugPeriod = 1.0f / Mathf.Max(0.1f, debugPrintHz);
            if (Time.realtimeSinceStartup >= nextDebugTime)
            {
                nextDebugTime += debugPeriod;
                PrintOrientationDebug();
            }
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
    }

    void HandleKeyboard()
    {
        float dt = Time.deltaTime;

        float dx = 0f;
        float dy = 0f;
        float dz = 0f;

        if (Input.GetKey(KeyCode.W)) dx += 1f;
        if (Input.GetKey(KeyCode.S)) dx -= 1f;

        if (Input.GetKey(KeyCode.A)) dz -= 1f;
        if (Input.GetKey(KeyCode.D)) dz += 1f;

        if (Input.GetKey(KeyCode.Q)) dy += 1f;
        if (Input.GetKey(KeyCode.E)) dy -= 1f;

        transform.position += new Vector3(dx, dy, dz) * (moveSpeed * dt);

        float yaw   = 0f;
        float pitch = 0f;
        float roll  = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))  yaw   -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) yaw   += 1f;
        if (Input.GetKey(KeyCode.UpArrow))    pitch  -= 1f;
        if (Input.GetKey(KeyCode.DownArrow))  pitch  += 1f;
        if (Input.GetKey(KeyCode.Z))          roll   -= 1f;
        if (Input.GetKey(KeyCode.X))          roll   += 1f;

        transform.Rotate(new Vector3(pitch, yaw, roll) * (rotateSpeedDeg * dt), Space.Self);
    }

    void OnEePoseReceived(PoseStampedMsg msg)
    {
        Vector3 rosPos = new Vector3(
            (float)msg.pose.position.x,
            (float)msg.pose.position.y,
            (float)msg.pose.position.z
        );

        Quaternion rosRot = new Quaternion(
            (float)msg.pose.orientation.x,
            (float)msg.pose.orientation.y,
            (float)msg.pose.orientation.z,
            (float)msg.pose.orientation.w
        );

        pendingUnityPosition  = RosToUnityPosition(rosPos);
        pendingUnityRotation  = RosToUnityRotation(rosRot);
        _latestEeUnity        = pendingUnityPosition;
        pendingEeApply        = true;
        lastEeReceiveTime     = Time.realtimeSinceStartup;
    }

    void ApplyPendingEePoseIfAny()
    {
        if (!pendingEeApply)
            return;

        pendingEeApply = false;

        if (haveInitialEe)
            return;

        if (useOffsetAlignment)
        {
            _alignOffset  = pendingUnityPosition - transform.position;
            haveInitialEe = true;
            Debug.Log($"[HandTarget] VR alignment offset applied: {_alignOffset:F3}");
        }
        else if (snapToRobotEeOnStart)
        {
            transform.position = pendingUnityPosition;
            if (useRobotEeRotationOnStart)
                transform.rotation = pendingUnityRotation;
            haveInitialEe = true;
            Debug.Log("HandTarget snapped to /robot/ee_pose");
        }
    }

    void PrintRawQuaternionDebug()
    {
        Quaternion qAbs = transform.rotation;

        Debug.Log(
            $"[UNITY RAW HAND] " +
            $"abs_u=({qAbs.x:F4}, {qAbs.y:F4}, {qAbs.z:F4}, {qAbs.w:F4})"
        );
    }

    void PrintOrientationDebug()
    {
        Quaternion qUnityAbs = transform.rotation;

        Matrix4x4 rosAbs = UnityQuatToRosMatrix(qUnityAbs);
        Vector3 rosAbsRpy = WrapEuler180(RosMatrixToRPYDeg(rosAbs));

        Vector3 unityWorldEuler = WrapEuler180(transform.eulerAngles);

        Debug.Log(
            $"[HandTarget DEBUG] " +
            $"unity_world_euler=({unityWorldEuler.x:F1}, {unityWorldEuler.y:F1}, {unityWorldEuler.z:F1}) " +
            $"ros_abs_rpy=({rosAbsRpy.x:F1}, {rosAbsRpy.y:F1}, {rosAbsRpy.z:F1})"
        );
    }

    void PublishTeleop()
    {
        ros.Publish(teleopTopic, new BoolMsg(true));
    }

    void PublishPoseStamped()
    {
        Vector3 p = useOffsetAlignment ? transform.position + _alignOffset : transform.position;
        Quaternion q = transform.rotation;

        var pos = new PointMsg(p.x, p.y, p.z);
        var ori = new QuaternionMsg(q.x, q.y, q.z, q.w);

        var header = new HeaderMsg(NowRosTime(), frameId);
        var pose = new PoseMsg(pos, ori);

        if (debugRawQuaternion)
        {
            Debug.Log(
                $"[UNITY RAW PUB] " +
                $"quat_u=({q.x:F4}, {q.y:F4}, {q.z:F4}, {q.w:F4})"
            );
        }

        ros.Publish(poseTopic, new PoseStampedMsg(header, pose));
    }

    static Vector3 RosToUnityPosition(Vector3 ros)
    {
        return new Vector3(-ros.y, ros.z, ros.x);
    }

    static Quaternion RosToUnityRotation(Quaternion rosQ)
    {
        return rosQ;
    }

    static Vector3 UnityToRosVector(Vector3 unityVec)
    {
        return new Vector3(unityVec.z, -unityVec.x, unityVec.y);
    }

    static Matrix4x4 UnityQuatToRosMatrix(Quaternion unityQ)
    {
        Vector3 uRight   = unityQ * Vector3.right;
        Vector3 uUp      = unityQ * Vector3.up;
        Vector3 uForward = unityQ * Vector3.forward;

        Vector3 rRight   = UnityToRosVector(uRight);
        Vector3 rUp      = UnityToRosVector(uUp);
        Vector3 rForward = UnityToRosVector(uForward);

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(rRight.x,   rRight.y,   rRight.z,   0f));
        m.SetColumn(1, new Vector4(rUp.x,       rUp.y,       rUp.z,     0f));
        m.SetColumn(2, new Vector4(rForward.x,  rForward.y,  rForward.z, 0f));

        return m;
    }

    static Vector3 RosMatrixToRPYDeg(Matrix4x4 m)
    {
        float r00 = m[0, 0];
        float r10 = m[1, 0];
        float r20 = m[2, 0];
        float r21 = m[2, 1];
        float r22 = m[2, 2];

        float sinp = -r20;

        float pitch = (Mathf.Abs(sinp) >= 1f)
            ? Mathf.Sign(sinp) * Mathf.PI * 0.5f
            : Mathf.Asin(sinp);

        float roll = Mathf.Atan2(r21, r22);
        float yaw  = Mathf.Atan2(r10, r00);

        return new Vector3(
            roll  * Mathf.Rad2Deg,
            pitch * Mathf.Rad2Deg,
            yaw   * Mathf.Rad2Deg
        );
    }

    static Vector3 WrapEuler180(Vector3 e)
    {
        return new Vector3(Wrap180(e.x), Wrap180(e.y), Wrap180(e.z));
    }

    static float Wrap180(float deg)
    {
        while (deg >  180f) deg -= 360f;
        while (deg < -180f) deg += 360f;
        return deg;
    }

    static TimeMsg NowRosTime()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        long sec  = now.ToUnixTimeSeconds();
        long ticksWithinSecond = now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond;
        long nsec = ticksWithinSecond * 100;

        if (nsec < 0)         nsec = 0;
        if (nsec > 999999999) nsec = 999999999;

        return new TimeMsg((int)sec, (uint)nsec);
    }
}
