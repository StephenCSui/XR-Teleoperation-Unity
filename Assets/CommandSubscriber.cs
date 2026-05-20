using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class CommandSubscriber : MonoBehaviour
{
    public string topic = "/unity/command_pose";
    public Transform commandBox;

    public bool applyRotation = true;
    public Quaternion rotationOffset = Quaternion.identity;

    private ROSConnection ros;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<PoseStampedMsg>(topic, OnPose);
    }

    void OnPose(PoseStampedMsg msg)
    {
        if (commandBox == null) return;

        var p = msg.pose.position;
        var q = msg.pose.orientation;

        Vector3 posRos = new Vector3((float)p.x, (float)p.y, (float)p.z);
        commandBox.position = RosToUnityPosition(posRos);

        if (applyRotation)
        {
            Quaternion rotRos = new Quaternion((float)q.x, (float)q.y, (float)q.z, (float)q.w);
            commandBox.rotation = RosToUnityRotation(rotRos) * rotationOffset;
        }
    }

    Vector3 RosToUnityPosition(Vector3 ros)
    {
        return new Vector3(-ros.y, ros.z, ros.x);
    }

    Quaternion NormalizeQuaternion(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

        if (mag < 1e-8f)
            return Quaternion.identity;

        float inv = 1.0f / mag;
        return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
    }

    Quaternion RosToUnityRotation(Quaternion rosQ)
    {
        rosQ = NormalizeQuaternion(rosQ);

        return NormalizeQuaternion(
            new Quaternion(
                -rosQ.y,
                 rosQ.z,
                 rosQ.x,
                -rosQ.w
            )
        );
    }
}
