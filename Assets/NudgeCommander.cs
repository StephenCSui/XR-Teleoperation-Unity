using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

/// <summary>
/// Sends discrete nudge commands to the ROS filter when PRECISION + detach mode is active.
/// Each key-down fires exactly one Twist message (edge-triggered, not held).
///
/// Position keys match HandTargetDemoPublisher convention so muscle memory transfers:
///   W/S  — Unity +Z/-Z  (→ ROS +X/-X, forward/back)
///   A/D  — Unity -X/+X  (→ ROS +Y/-Y, left/right)
///   Q/E  — Unity +Y/-Y  (→ ROS +Z/-Z, up/down)
///
/// Orientation keys (ROS world frame — filter pre-multiplies):
///   ← →  — yaw  left/right
///   ↑ ↓  — pitch up/down
///   Z/X  — roll  CCW/CW
///
/// Note: HandTargetDemoPublisher reads the same position keys and will move the
/// virtual hand object, but the ROS filter ignores hand tracking while detached,
/// so only nudge commands affect the robot.
/// </summary>
public class NudgeCommander : MonoBehaviour
{
    [Header("ROS Topic")]
    public string nudgeTopic = "/unity/nudge_cmd";

    [Header("References")]
    public TeleopModeController modeController;

    private ROSConnection _ros;

    void Start()
    {
        if (modeController == null)
            Debug.LogWarning("[NudgeCommander] modeController not assigned — nudges will never fire.");

        _ros = ROSConnection.GetOrCreateInstance();
        _ros.RegisterPublisher<TwistMsg>(nudgeTopic);
    }

    void Update()
    {
        if (modeController == null)           return;
        if (!modeController.IsPrecision)      return;

        // Accumulate all keys pressed this frame into one message.
        // GetKeyDown = true only on the first frame the key is pressed.
        double lx = 0, ly = 0, lz = 0;
        double ax = 0, ay = 0, az = 0;

#if !UNITY_ANDROID
        // Position (Unity frame — filter applies unity_to_ros_delta)
        if (Input.GetKeyDown(KeyCode.W)) lz += 1.0;
        if (Input.GetKeyDown(KeyCode.S)) lz -= 1.0;
        if (Input.GetKeyDown(KeyCode.A)) lx -= 1.0;
        if (Input.GetKeyDown(KeyCode.D)) lx += 1.0;
        if (Input.GetKeyDown(KeyCode.Q)) ly += 1.0;
        if (Input.GetKeyDown(KeyCode.E)) ly -= 1.0;

        // Orientation (ROS world frame — filter pre-multiplies)
        if (Input.GetKeyDown(KeyCode.LeftArrow))  az -= 1.0;
        if (Input.GetKeyDown(KeyCode.RightArrow)) az += 1.0;
        if (Input.GetKeyDown(KeyCode.UpArrow))    ay += 1.0;
        if (Input.GetKeyDown(KeyCode.DownArrow))  ay -= 1.0;
        if (Input.GetKeyDown(KeyCode.Z))          ax -= 1.0;
        if (Input.GetKeyDown(KeyCode.X))          ax += 1.0;
#endif

        if (lx == 0 && ly == 0 && lz == 0 && ax == 0 && ay == 0 && az == 0)
            return;

        var msg = new TwistMsg
        {
            linear  = new Vector3Msg { x = lx, y = ly, z = lz },
            angular = new Vector3Msg { x = ax, y = ay, z = az },
        };
        _ros.Publish(nudgeTopic, msg);
    }
}
