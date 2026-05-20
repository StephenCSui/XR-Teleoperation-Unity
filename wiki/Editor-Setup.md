# Editor Setup & Debugging

## Platform Switching — Keyboard vs Headset

The project uses `#if !UNITY_ANDROID` guards throughout to provide keyboard fallback inputs when testing in the editor without a headset.

| Build Target | `UNITY_ANDROID` defined | Keyboard inputs | XR controller inputs |
|---|---|---|---|
| **Standalone** (PC) | No | Active | Inactive |
| **Android** | Yes | Compiled out | Active |

**To test with keyboard (no headset):**

1. Open **File → Build Settings**.
2. Select **PC, Mac & Linux Standalone** and click **Switch Platform**.
3. Play in the Editor — keyboard controls are now active.

**To build for Quest:**

1. Open **File → Build Settings**.
2. Select **Android** and click **Switch Platform**.
3. Build as described in [Building](Building.md).

> Always switch back to Android before building the APK. Forgetting this is the most common cause of a build that has keyboard controls compiled in but no XR input.

## Keyboard Controls (Standalone only)

| Key | Action |
|-----|--------|
| `T` | Toggle teleoperation on/off |
| `P` | Pen down (hold) / Enter or exit PRECISION mode |
| `Tab` | Toggle position / orientation in PRECISION (= B button) |
| `W / S` | EE forward / back (Unity +Z/-Z) |
| `A / D` | EE left / right (Unity -X/+X) |
| `Q / E` | EE up / down (Unity +Y/-Y) |
| `← →` | Yaw left / right |
| `↑ ↓` | Pitch up / down |
| `Z / X` | Roll CCW / CW |

WASDQE and arrow keys only fire nudge commands when in PRECISION mode.

## ROS IP Setup

The ROS IP is set in the Inspector on the `ROSConnection` component (usually on a GameObject called **ROSTCPConnector** in the Hierarchy). This value is baked into the APK at build time.

To find your laptop WiFi IP:
```bash
ip addr show | grep "inet " | grep -v 127
```

Use the IP on the same interface the Quest is on (typically `wlan0` or `wlp*`).

## Checking Topics in the Editor

When testing in Standalone mode with ROS running on the same machine or LAN:

```bash
# Confirm Unity is publishing hand pose
ros2 topic hz /unity/hand_pose

# Check all Unity-side topics
ros2 topic list | grep unity
```

## HUD

The in-headset HUD (rendered via `OnGUI`) shows:

- Current mode: `NORMAL` or `PRECISION`
- In PRECISION: active control (`POSITION` or `ORIENTATION`) and exit hint

The HUD is always on. No setting needs to be changed to enable it.

## Common Editor Issues

**XR device not found in Standalone play mode**
The editor is on Standalone with OpenXR loader active. Either plug in a compatible controller, or disable **XR Plugin Management → OpenXR** for Standalone while testing with keyboard only (re-enable before switching back to Android).

**`ROSConnection` fails to connect in editor**
ROS is not running, or the IP/port is wrong. Check that `ros2 launch ur_unity_bringup ...` is running and the ROS-TCP endpoint is up (`ros2 node list | grep endpoint`).

**Teleop enabled but EE does not move**
The snap has not fired yet — the editor is waiting for the first `/robot/ee_pose` message. Check `ros2 topic hz /robot/ee_pose` to confirm the ROS side is publishing.
